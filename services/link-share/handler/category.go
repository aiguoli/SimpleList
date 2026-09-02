package handler

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"os"
)

func GetCategories(c *fiber.Ctx) error {
	db := database.DB
	var categories []models.Category
	db.Find(&categories)
	return c.JSON(fiber.Map{
		"code": 200,
		"data": categories,
	})
}

func CreateCategory(c *fiber.Ctx) error {
	db := database.DB
	userId := c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"].(float64)
	var user models.User
	if err := db.Find(&user, userId).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"code": 500,
			"msg":  "User not found!",
		})
	}
	if user.Email != os.Getenv("ADMIN_EMAIL") {
		return c.Status(403).JSON(fiber.Map{
			"code": 403,
			"msg":  "Forbidden",
		})
	}
	var category models.Category
	if err := c.BodyParser(&category); err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't parse JSON",
		})
	}
	db.Create(&category)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Category created successfully",
		"data": category,
	})
}

func UpdateCategory(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB
	userId := c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"].(float64)
	var user models.User
	if err := db.Find(&user, userId).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"code": 500,
			"msg":  "User not found!",
		})
	}
	if user.Email != os.Getenv("ADMIN_EMAIL") {
		return c.Status(403).JSON(fiber.Map{
			"code": 403,
			"msg":  "Forbidden",
		})
	}
	var category models.Category
	var newCategory models.Category
	if err := c.BodyParser(&newCategory); err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't parse JSON",
		})
	}
	db.First(&category, id)
	category.Name = newCategory.Name
	db.Save(&category)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Category updated successfully",
		"data": category,
	})
}

func DeleteCategory(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB
	userId := c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"].(float64)
	var user models.User
	if err := db.Find(&user, userId).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"code": 500,
			"msg":  "User not found!",
		})
	}
	if user.Email != os.Getenv("ADMIN_EMAIL") {
		return c.Status(403).JSON(fiber.Map{
			"code": 403,
			"msg":  "Forbidden",
		})
	}
	var category models.Category
	db.First(&category, id)
	db.Delete(&category)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Category deleted successfully",
	})
}
