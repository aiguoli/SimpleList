package middleware

import (
	"errors"
	jwtware "github.com/gofiber/contrib/jwt"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"os"
)

const MinimumJWTSecretLength = 32

func ValidateConfiguration() error {
	if len(os.Getenv("JWT_SECRET")) < MinimumJWTSecretLength {
		return errors.New("JWT_SECRET must contain at least 32 characters")
	}
	return nil
}

func Protected() fiber.Handler {
	return jwtware.New(jwtware.Config{
		SigningKey: jwtware.SigningKey{Key: []byte(os.Getenv("JWT_SECRET"))},
	})
}

// RequireAccessToken prevents refresh or other future token types from being
// accepted as API access tokens. Tokens issued by the legacy v1 endpoint do
// not contain token_type and remain valid on v1 routes during the transition.
func RequireAccessToken(c *fiber.Ctx) error {
	token, ok := c.Locals("user").(*jwt.Token)
	if !ok || token == nil {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{"code": fiber.StatusUnauthorized, "message": "Unauthorized"})
	}
	claims, ok := token.Claims.(jwt.MapClaims)
	if !ok || claims["token_type"] != "access" {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{"code": fiber.StatusUnauthorized, "message": "Invalid access token"})
	}
	return c.Next()
}
