package handler

import (
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"net/mail"
	"os"
	"strings"
	"time"
	"unicode/utf8"

	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"golang.org/x/crypto/bcrypt"
	"gorm.io/gorm"
)

const (
	accessTokenLifetime  = 30 * time.Minute
	refreshTokenLifetime = 30 * 24 * time.Hour
)

type authV2Request struct {
	Email    string `json:"email"`
	Username string `json:"username"`
	Password string `json:"password"`
}

type refreshV2Request struct {
	RefreshToken string `json:"refresh_token"`
}

type userV2 struct {
	ID       uint   `json:"id"`
	Email    string `json:"email"`
	Username string `json:"username"`
}

type authV2Response struct {
	AccessToken  string    `json:"access_token"`
	RefreshToken string    `json:"refresh_token"`
	ExpiresAt    time.Time `json:"expires_at"`
	User         userV2    `json:"user"`
}

func RegisterV2(c *fiber.Ctx) error {
	var request authV2Request
	if err := c.BodyParser(&request); err != nil {
		return errorV2(c, fiber.StatusBadRequest, "Couldn't parse request")
	}
	request.Email = strings.ToLower(strings.TrimSpace(request.Email))
	request.Username = strings.TrimSpace(request.Username)
	if !validEmail(request.Email) || utf8.RuneCountInString(request.Username) < 3 || len(request.Password) < 8 {
		return errorV2(c, fiber.StatusBadRequest, "A valid email, username of at least 3 characters, and password of at least 8 characters are required")
	}

	hash, err := bcrypt.GenerateFromPassword([]byte(request.Password), bcrypt.DefaultCost)
	if err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't create account")
	}
	user := models.User{Email: request.Email, Username: request.Username, Password: string(hash)}
	var response authV2Response
	err = database.DB.Transaction(func(tx *gorm.DB) error {
		if err := tx.Create(&user).Error; err != nil {
			return err
		}
		issued, err := issueSessionV2(tx, user)
		response = issued
		return err
	})
	if err != nil {
		return errorV2(c, fiber.StatusConflict, "Email or username is already registered")
	}
	return c.Status(fiber.StatusCreated).JSON(fiber.Map{"data": response})
}

func LoginV2(c *fiber.Ctx) error {
	var request authV2Request
	if err := c.BodyParser(&request); err != nil {
		return errorV2(c, fiber.StatusBadRequest, "Couldn't parse request")
	}
	email := strings.ToLower(strings.TrimSpace(request.Email))
	var user models.User
	if err := database.DB.Where("email = ?", email).First(&user).Error; err != nil || bcrypt.CompareHashAndPassword([]byte(user.Password), []byte(request.Password)) != nil {
		return errorV2(c, fiber.StatusUnauthorized, "Invalid email or password")
	}
	response, err := issueSessionV2(database.DB, user)
	if err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't create session")
	}
	return okV2(c, response)
}

func RefreshV2(c *fiber.Ctx) error {
	var request refreshV2Request
	if err := c.BodyParser(&request); err != nil || strings.TrimSpace(request.RefreshToken) == "" {
		return errorV2(c, fiber.StatusBadRequest, "refresh_token is required")
	}

	var response authV2Response
	err := database.DB.Transaction(func(tx *gorm.DB) error {
		var stored models.RefreshToken
		if err := tx.Where("token_hash = ? AND expires_at > ?", hashRefreshTokenV2(request.RefreshToken), time.Now().UTC()).First(&stored).Error; err != nil {
			return err
		}
		var user models.User
		if err := tx.First(&user, stored.UserID).Error; err != nil {
			return err
		}
		if err := tx.Delete(&stored).Error; err != nil {
			return err
		}
		issued, err := issueSessionV2(tx, user)
		response = issued
		return err
	})
	if err != nil {
		return errorV2(c, fiber.StatusUnauthorized, "Refresh token is invalid or expired")
	}
	return okV2(c, response)
}

func LogoutV2(c *fiber.Ctx) error {
	var request refreshV2Request
	if err := c.BodyParser(&request); err != nil || strings.TrimSpace(request.RefreshToken) == "" {
		return errorV2(c, fiber.StatusBadRequest, "refresh_token is required")
	}
	if err := database.DB.Where("token_hash = ?", hashRefreshTokenV2(request.RefreshToken)).Delete(&models.RefreshToken{}).Error; err != nil {
		return errorV2(c, fiber.StatusInternalServerError, "Couldn't end session")
	}
	return c.SendStatus(fiber.StatusNoContent)
}

func MeV2(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return errorV2(c, fiber.StatusUnauthorized, "Unauthorized")
	}
	var user models.User
	if err := database.DB.First(&user, userID).Error; err != nil {
		return errorV2(c, fiber.StatusNotFound, "User not found")
	}
	return okV2(c, toUserV2(user))
}

func issueSessionV2(db *gorm.DB, user models.User) (authV2Response, error) {
	expiresAt := time.Now().UTC().Add(accessTokenLifetime)
	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"sub":        user.ID,
		"user_id":    user.ID,
		"username":   user.Username,
		"token_type": "access",
		"iat":        time.Now().UTC().Unix(),
		"exp":        expiresAt.Unix(),
	})
	accessToken, err := token.SignedString([]byte(os.Getenv("JWT_SECRET")))
	if err != nil {
		return authV2Response{}, err
	}

	refreshBytes := make([]byte, 32)
	if _, err := rand.Read(refreshBytes); err != nil {
		return authV2Response{}, err
	}
	refreshToken := hex.EncodeToString(refreshBytes)
	stored := models.RefreshToken{
		UserID: user.ID, TokenHash: hashRefreshTokenV2(refreshToken), ExpiresAt: time.Now().UTC().Add(refreshTokenLifetime),
	}
	if err := db.Create(&stored).Error; err != nil {
		return authV2Response{}, err
	}
	return authV2Response{
		AccessToken: accessToken, RefreshToken: refreshToken, ExpiresAt: expiresAt, User: toUserV2(user),
	}, nil
}

func hashRefreshTokenV2(value string) string {
	hash := sha256.Sum256([]byte(value))
	return hex.EncodeToString(hash[:])
}

func validEmail(value string) bool {
	address, err := mail.ParseAddress(value)
	return err == nil && strings.EqualFold(address.Address, value)
}

func toUserV2(user models.User) userV2 {
	return userV2{ID: user.ID, Email: user.Email, Username: user.Username}
}
