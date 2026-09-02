package handler

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
)

func CollectV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}
	var link models.Link
	if err := database.DB.First(&link, c.Params("id")).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "Link not found")
	}
	collection := models.Collection{UserID: userID, ShareID: link.ID}
	if err := database.DB.Where("user_id = ? AND share_id = ?", userID, link.ID).FirstOrCreate(&collection).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't collect link")
	}
	return okV2(c, toLinkV2(link))
}

func UncollectV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}
	if err := database.DB.Where("user_id = ? AND share_id = ?", userID, c.Params("id")).Delete(&models.Collection{}).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't uncollect link")
	}
	return c.SendStatus(fiber.StatusNoContent)
}

func CollectionsV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}
	var links []models.Link
	if err := database.DB.Model(&models.Link{}).
		Joins("JOIN collections ON collections.share_id = links.id AND collections.deleted_at IS NULL").
		Where("collections.user_id = ?", userID).
		Order("collections.created_at DESC").
		Find(&links).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't load collections")
	}
	result := make([]linkV2, 0, len(links))
	for _, link := range links {
		result = append(result, toLinkV2(link))
	}
	return okV2(c, result)
}
