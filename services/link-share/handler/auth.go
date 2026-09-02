package handler

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"golang.org/x/crypto/bcrypt"
	"os"
	"strings"
	"time"
)

type signupInput struct {
	Email    string `json:"email"`
	Username string `json:"username"`
	Password string `json:"password"`
}

type loginInput struct {
	Email    string `json:"email"`
	Password string `json:"password"`
}

func Register(c *fiber.Ctx) error {
	db := database.DB
	req := new(signupInput)
	if err := c.BodyParser(req); err != nil {
		return c.Status(400).JSON(fiber.Map{
			"code": 400,
			"msg":  "Couldn't parse JSON",
		})
	}
	req.Email = strings.ToLower(strings.TrimSpace(req.Email))
	req.Username = strings.TrimSpace(req.Username)
	if !validEmail(req.Email) || len(req.Username) < 3 || len(req.Password) < 8 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{"code": 400, "msg": "Invalid registration details"})
	}
	hashedPassword, err := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)
	if err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't hash password",
		})
	}
	user := &models.User{
		Email:    req.Email,
		Username: req.Username,
		Password: string(hashedPassword),
	}
	if err := db.Create(&user).Error; err != nil {
		return c.Status(fiber.StatusConflict).JSON(fiber.Map{"code": 409, "msg": "Email or username is already registered"})
	}
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "User created successfully",
		"data": user,
	})
}

func Login(c *fiber.Ctx) error {
	db := database.DB
	req := new(loginInput)
	if err := c.BodyParser(req); err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't parse JSON",
		})
	}
	user := new(models.User)
	if err := db.Where("email = ?", strings.ToLower(strings.TrimSpace(req.Email))).First(user).Error; err != nil {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{
			"code": 401,
			"msg":  "Invalid email or password",
		})
	}
	if !CheckPasswordHash(req.Password, user.Password) {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{
			"code": 401,
			"msg":  "Invalid email or password",
		})
	}
	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"username": user.Username,
		"user_id":  user.ID,
		"exp":      time.Now().Add(time.Hour * 72).Unix(),
	})
	s, err := token.SignedString([]byte(os.Getenv("JWT_SECRET")))
	if err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't generate token",
		})
	}
	return c.JSON(fiber.Map{
		"code":    200,
		"token":   s,
		"expires": time.Now().Add(time.Hour * 72),
	})
}

func CheckPasswordHash(password, hash string) bool {
	err := bcrypt.CompareHashAndPassword([]byte(hash), []byte(password))
	return err == nil
}
