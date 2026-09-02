package handler

import (
	"fmt"
	"net/url"
	"strings"
	"time"

	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"gorm.io/gorm"
)

type providerCapabilitiesV2 struct {
	PublicShare      bool `json:"public_share"`
	CommunityPublish bool `json:"community_publish"`
	Password         bool `json:"password"`
	Expiration       bool `json:"expiration"`
}

type providerV2 struct {
	Type         string                 `json:"type"`
	DisplayName  string                 `json:"display_name"`
	Capabilities providerCapabilitiesV2 `json:"capabilities"`
}

type linkV2 struct {
	ID           uint       `json:"id"`
	CreatedAt    time.Time  `json:"created_at"`
	UpdatedAt    time.Time  `json:"updated_at"`
	Title        string     `json:"title"`
	URL          string     `json:"url"`
	Password     string     `json:"password"`
	ExpiresAt    *time.Time `json:"expires_at"`
	ProviderType string     `json:"provider_type"`
	UserID       uint       `json:"user_id"`
	Views        uint       `json:"views"`
}

type createLinkV2Request struct {
	Title        string `json:"title" form:"title"`
	URL          string `json:"url" form:"url"`
	Password     string `json:"password" form:"password"`
	ExpiresAt    string `json:"expires_at" form:"expires_at"`
	ProviderType string `json:"provider_type" form:"provider_type"`
}

type updateLinkV2Request struct {
	Title        *string `json:"title"`
	URL          *string `json:"url"`
	Password     *string `json:"password"`
	ExpiresAt    *string `json:"expires_at"`
	ProviderType *string `json:"provider_type"`
}

func GetProvidersV2(c *fiber.Ctx) error {
	return okV2(c, []providerV2{
		{
			Type:        models.ProviderOneDrive,
			DisplayName: "OneDrive",
			Capabilities: providerCapabilitiesV2{
				PublicShare: true, CommunityPublish: true, Password: true, Expiration: true,
			},
		},
		{
			Type:        models.ProviderGoogleDrive,
			DisplayName: "Google Drive",
			Capabilities: providerCapabilitiesV2{
				PublicShare: true, CommunityPublish: true, Password: false, Expiration: false,
			},
		},
		{
			Type: models.ProviderPikPak, DisplayName: "PikPak",
			Capabilities: providerCapabilitiesV2{},
		},
		{
			Type: models.ProviderLocal, DisplayName: "Local",
			Capabilities: providerCapabilitiesV2{},
		},
	})
}

func GetLinksV2(c *fiber.Ctx) error {
	db := database.DB
	query := db.Model(&models.Link{}).Order("created_at DESC")

	if requestedProvider := c.Query("provider_type"); requestedProvider != "" {
		providerType := models.NormalizeProviderType(requestedProvider)
		if !models.IsSupportedProvider(providerType) {
			return errorV2(c, fiber.StatusBadRequest, "Unsupported provider_type")
		}
		query = query.Where("provider_type = ?", providerType)
	}

	var links []models.Link
	if err := query.Find(&links).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't load links")
	}

	response := make([]linkV2, 0, len(links))
	for _, link := range links {
		response = append(response, toLinkV2(link))
	}
	return okV2(c, response)
}

func GetLinkV2(c *fiber.Ctx) error {
	var link models.Link
	if err := database.DB.First(&link, c.Params("id")).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "Link not found")
	}
	return okV2(c, toLinkV2(link))
}

func CreateLinkV2(c *fiber.Ctx) error {
	var request createLinkV2Request
	if err := c.BodyParser(&request); err != nil {
		return errorV2(c, fiber.StatusBadRequest, "Couldn't parse request")
	}

	request.Title = strings.TrimSpace(request.Title)
	request.URL = strings.TrimSpace(request.URL)
	request.ProviderType = models.NormalizeProviderType(request.ProviderType)
	if request.Title == "" || !isPublicHTTPURL(request.URL) {
		return errorV2(c, fiber.StatusBadRequest, "title and a valid http(s) url are required")
	}
	if !models.IsSupportedProvider(request.ProviderType) {
		return errorV2(c, fiber.StatusBadRequest, "Unsupported provider_type")
	}

	expiresAt, err := parseExpirationV2(request.ExpiresAt)
	if err != nil {
		return errorV2(c, fiber.StatusBadRequest, "expires_at must be an RFC3339 timestamp or YYYY-MM-DD date")
	}
	if err := validateProviderOptionsV2(request.ProviderType, request.Password, expiresAt); err != nil {
		return errorV2(c, fiber.StatusBadRequest, err.Error())
	}

	link := models.Link{
		Title:          request.Title,
		Content:        request.URL,
		Password:       request.Password,
		ExpirationDate: expiresAt,
		ProviderType:   request.ProviderType,
	}
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}
	link.UserID = userID
	if err := database.DB.Create(&link).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't create link")
	}
	return c.Status(fiber.StatusCreated).JSON(fiber.Map{"code": fiber.StatusCreated, "data": toLinkV2(link)})
}

func IncrementViewsV2(c *fiber.Ctx) error {
	var link models.Link
	if err := database.DB.First(&link, c.Params("id")).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "Link not found")
	}
	if err := database.DB.Model(&link).UpdateColumn("views", gorm.Expr("views + ?", 1)).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't increment views")
	}
	link.Views++
	return okV2(c, toLinkV2(link))
}

func UpdateLinkV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}

	var link models.Link
	if err := database.DB.First(&link, "id = ? AND user_id = ?", c.Params("id"), userID).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "Link not found")
	}

	var request updateLinkV2Request
	if err := c.BodyParser(&request); err != nil {
		return errorV2(c, fiber.StatusBadRequest, "Couldn't parse request")
	}
	if request.Title != nil {
		link.Title = strings.TrimSpace(*request.Title)
		if link.Title == "" {
			return errorV2(c, fiber.StatusBadRequest, "title cannot be empty")
		}
	}
	if request.URL != nil {
		link.Content = strings.TrimSpace(*request.URL)
		if !isPublicHTTPURL(link.Content) {
			return errorV2(c, fiber.StatusBadRequest, "url must use http or https")
		}
	}
	if request.Password != nil {
		link.Password = *request.Password
	}
	if request.ExpiresAt != nil {
		expiresAt, err := parseExpirationV2(*request.ExpiresAt)
		if err != nil {
			return errorV2(c, fiber.StatusBadRequest, "expires_at must be an RFC3339 timestamp or YYYY-MM-DD date")
		}
		link.ExpirationDate = expiresAt
	}
	if request.ProviderType != nil {
		providerType := models.NormalizeProviderType(*request.ProviderType)
		if !models.IsSupportedProvider(providerType) {
			return errorV2(c, fiber.StatusBadRequest, "Unsupported provider_type")
		}
		link.ProviderType = providerType
	}
	if err := validateProviderOptionsV2(link.ProviderType, link.Password, link.ExpirationDate); err != nil {
		return errorV2(c, fiber.StatusBadRequest, err.Error())
	}

	if err := database.DB.Save(&link).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't update link")
	}
	return okV2(c, toLinkV2(link))
}

func DeleteLinkV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}

	var link models.Link
	if err := database.DB.First(&link, "id = ? AND user_id = ?", c.Params("id"), userID).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "Link not found")
	}
	if err := database.DB.Delete(&link).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't delete link")
	}
	return c.SendStatus(fiber.StatusNoContent)
}

func toLinkV2(link models.Link) linkV2 {
	providerType := models.NormalizeProviderType(link.ProviderType)
	if providerType == "" {
		providerType = models.ProviderOneDrive
	}

	var expiresAt *time.Time
	if !link.ExpirationDate.IsZero() {
		value := link.ExpirationDate
		expiresAt = &value
	}
	return linkV2{
		ID:           link.ID,
		CreatedAt:    link.CreatedAt,
		UpdatedAt:    link.UpdatedAt,
		Title:        link.Title,
		URL:          link.Content,
		Password:     link.Password,
		ExpiresAt:    expiresAt,
		ProviderType: providerType,
		UserID:       link.UserID,
		Views:        link.Views,
	}
}

func parseExpirationV2(value string) (time.Time, error) {
	value = strings.TrimSpace(value)
	if value == "" {
		return time.Time{}, nil
	}
	if parsed, err := time.Parse(time.RFC3339, value); err == nil {
		return parsed, nil
	}
	return time.Parse("2006-01-02", value)
}

func isPublicHTTPURL(value string) bool {
	parsed, err := url.ParseRequestURI(value)
	return err == nil && parsed.Host != "" && (parsed.Scheme == "http" || parsed.Scheme == "https")
}

func validateProviderOptionsV2(providerType, password string, expiresAt time.Time) error {
	switch models.NormalizeProviderType(providerType) {
	case models.ProviderOneDrive:
		return nil
	case models.ProviderGoogleDrive:
		if password != "" || !expiresAt.IsZero() {
			return fmt.Errorf("google_drive does not support password or expiration")
		}
		return nil
	default:
		return fmt.Errorf("provider does not support community publishing")
	}
}

func currentUserID(c *fiber.Ctx) (uint, bool) {
	token, ok := c.Locals("user").(*jwt.Token)
	if !ok || token == nil {
		return 0, false
	}
	claims, ok := token.Claims.(jwt.MapClaims)
	if !ok {
		return 0, false
	}
	switch value := claims["user_id"].(type) {
	case float64:
		return uint(value), true
	case uint:
		return value, true
	case int:
		return uint(value), value >= 0
	default:
		return 0, false
	}
}

func okV2(c *fiber.Ctx, data any) error {
	return c.JSON(fiber.Map{"code": fiber.StatusOK, "data": data})
}

func errorV2(c *fiber.Ctx, status int, message string) error {
	return c.Status(status).JSON(fiber.Map{"code": status, "message": message})
}
