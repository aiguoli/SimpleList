package handler

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/glebarez/sqlite"
	"github.com/gofiber/fiber/v2"
	"gorm.io/gorm"
)

func TestRegisterAndRotateRefreshTokenV2(t *testing.T) {
	t.Setenv("JWT_SECRET", strings.Repeat("s", 40))
	dsn := fmt.Sprintf("file:%s?mode=memory&cache=shared", t.Name())
	db, err := gorm.Open(sqlite.Open(dsn), &gorm.Config{})
	if err != nil {
		t.Fatal(err)
	}
	if err := db.AutoMigrate(&models.User{}, &models.RefreshToken{}); err != nil {
		t.Fatal(err)
	}
	database.DB = db

	app := fiber.New()
	app.Post("/register", RegisterV2)
	app.Post("/refresh", RefreshV2)

	registerBody := []byte(`{"email":"USER@example.com","username":"tester","password":"password123"}`)
	request := httptest.NewRequest("POST", "/register", bytes.NewReader(registerBody))
	request.Header.Set(fiber.HeaderContentType, fiber.MIMEApplicationJSON)
	response, err := app.Test(request)
	if err != nil {
		t.Fatal(err)
	}
	if response.StatusCode != fiber.StatusCreated {
		t.Fatalf("register status = %d", response.StatusCode)
	}
	var registered struct {
		Data authV2Response `json:"data"`
	}
	if err := json.NewDecoder(response.Body).Decode(&registered); err != nil {
		t.Fatal(err)
	}
	if registered.Data.RefreshToken == "" || registered.Data.AccessToken == "" || registered.Data.User.Email != "user@example.com" {
		t.Fatalf("unexpected registration response: %+v", registered.Data)
	}

	refreshBody := []byte(fmt.Sprintf(`{"refresh_token":%q}`, registered.Data.RefreshToken))
	refreshRequest := httptest.NewRequest("POST", "/refresh", bytes.NewReader(refreshBody))
	refreshRequest.Header.Set(fiber.HeaderContentType, fiber.MIMEApplicationJSON)
	refreshResponse, err := app.Test(refreshRequest)
	if err != nil {
		t.Fatal(err)
	}
	if refreshResponse.StatusCode != fiber.StatusOK {
		t.Fatalf("refresh status = %d", refreshResponse.StatusCode)
	}
	var refreshed struct {
		Data authV2Response `json:"data"`
	}
	if err := json.NewDecoder(refreshResponse.Body).Decode(&refreshed); err != nil {
		t.Fatal(err)
	}
	if refreshed.Data.RefreshToken == registered.Data.RefreshToken {
		t.Fatal("expected refresh token rotation")
	}

	reusedRequest := httptest.NewRequest("POST", "/refresh", bytes.NewReader(refreshBody))
	reusedRequest.Header.Set(fiber.HeaderContentType, fiber.MIMEApplicationJSON)
	reusedResponse, err := app.Test(reusedRequest)
	if err != nil {
		t.Fatal(err)
	}
	if reusedResponse.StatusCode != fiber.StatusUnauthorized {
		t.Fatalf("reused token status = %d", reusedResponse.StatusCode)
	}
}
