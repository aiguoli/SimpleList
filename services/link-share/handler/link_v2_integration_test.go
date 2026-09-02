package handler

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http/httptest"
	"testing"

	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/glebarez/sqlite"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"gorm.io/gorm"
)

func TestCreateAndFilterLinksV2(t *testing.T) {
	app := newLinkV2TestApp(t)
	body := []byte(`{"title":"Shared document","url":"https://drive.google.com/file/d/123","provider_type":"google_drive"}`)
	request := httptest.NewRequest("POST", "/api/v2/links", bytes.NewReader(body))
	request.Header.Set(fiber.HeaderContentType, fiber.MIMEApplicationJSON)
	response, err := app.Test(request)
	if err != nil {
		t.Fatal(err)
	}
	if response.StatusCode != fiber.StatusCreated {
		t.Fatalf("create status = %d, want %d", response.StatusCode, fiber.StatusCreated)
	}

	listRequest := httptest.NewRequest("GET", "/api/v2/links?provider_type=Google%20Drive", nil)
	listResponse, err := app.Test(listRequest)
	if err != nil {
		t.Fatal(err)
	}
	var payload struct {
		Code int      `json:"code"`
		Data []linkV2 `json:"data"`
	}
	if err := json.NewDecoder(listResponse.Body).Decode(&payload); err != nil {
		t.Fatal(err)
	}
	if payload.Code != fiber.StatusOK || len(payload.Data) != 1 {
		t.Fatalf("unexpected list response: code=%d links=%d", payload.Code, len(payload.Data))
	}
	if payload.Data[0].ProviderType != models.ProviderGoogleDrive {
		t.Fatalf("provider_type = %q", payload.Data[0].ProviderType)
	}
}

func TestCreateLinkV2RejectsUnsupportedProvider(t *testing.T) {
	app := newLinkV2TestApp(t)
	body := []byte(`{"title":"Local file","url":"https://example.com/file","provider_type":"local"}`)
	request := httptest.NewRequest("POST", "/api/v2/links", bytes.NewReader(body))
	request.Header.Set(fiber.HeaderContentType, fiber.MIMEApplicationJSON)
	response, err := app.Test(request)
	if err != nil {
		t.Fatal(err)
	}
	if response.StatusCode != fiber.StatusBadRequest {
		t.Fatalf("status = %d, want %d", response.StatusCode, fiber.StatusBadRequest)
	}
}

func newLinkV2TestApp(t *testing.T) *fiber.App {
	t.Helper()
	dsn := fmt.Sprintf("file:%s?mode=memory&cache=shared", t.Name())
	db, err := gorm.Open(sqlite.Open(dsn), &gorm.Config{})
	if err != nil {
		t.Fatal(err)
	}
	if err := db.AutoMigrate(&models.Link{}); err != nil {
		t.Fatal(err)
	}
	database.DB = db

	app := fiber.New()
	app.Use(func(c *fiber.Ctx) error {
		c.Locals("user", jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
			"user_id": float64(1), "token_type": "access",
		}))
		return c.Next()
	})
	app.Post("/api/v2/links", CreateLinkV2)
	app.Get("/api/v2/links", GetLinksV2)
	return app
}
