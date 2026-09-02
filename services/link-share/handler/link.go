package handler

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"time"
)

func GetLinks(c *fiber.Ctx) error {
	db := database.DB
	var links []models.Link
	db.Find(&links)
	return c.JSON(fiber.Map{
		"code": 200,
		"data": links,
	})
}

func GetLink(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB
	var link models.Link
	if err := db.Find(&link, id).Error; err != nil {
		return c.Status(404).JSON(fiber.Map{
			"code": 404,
			"msg":  "Link not found",
		})
	}
	return c.JSON(fiber.Map{
		"code": 200,
		"data": link,
	})
}

func CreateLink(c *fiber.Ctx) error {
	db := database.DB
	link := new(models.Link)
	if err := c.BodyParser(link); err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't parse JSON",
		})
	}
	if c.Locals("user") != nil {
		link.UserID = uint(c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"].(float64))
	}
	if link.ProviderType == "" {
		link.ProviderType = models.ProviderOneDrive
	}
	link.ExpirationDate, _ = time.Parse("2006-01-02", c.FormValue("expiration_date"))
	db.Create(&link)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Link created successfully",
		"data": link,
	})
}

func IncrementViews(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB
	var link models.Link
	if err := db.First(&link, id).Error; err != nil {
		return c.Status(404).JSON(fiber.Map{
			"code": 404,
			"msg":  "Link not found",
		})
	}
	link.Views++
	db.Save(&link)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Link views incremented",
		"data": link,
	})
}

func UpdateLink(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB

	var link models.Link
	var newLink models.Link

	if err := c.BodyParser(&newLink); err != nil {
		return c.Status(500).JSON(fiber.Map{
			"code": 500,
			"msg":  "Couldn't parse JSON",
		})
	}
	userId := c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"]
	if err := db.First(&link, "id = ? AND user_id = ?", id, userId).Error; err != nil {
		return c.Status(404).JSON(fiber.Map{
			"code": 404,
			"msg":  "Link not found",
		})
	}
	db.Model(&link).Omit("UserID").Updates(newLink)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Link updated successfully",
		"data": link,
	})
}

func DeleteLink(c *fiber.Ctx) error {
	id := c.Params("id")
	db := database.DB

	var link models.Link
	userId := c.Locals("user").(*jwt.Token).Claims.(jwt.MapClaims)["user_id"]
	if err := db.First(&link, "id = ? AND user_id = ?", id, userId).Error; err != nil {
		return c.Status(404).JSON(fiber.Map{
			"code": 404,
			"msg":  "Link not found",
		})
	}
	db.Delete(&link)
	return c.JSON(fiber.Map{
		"code": 200,
		"msg":  "Link deleted successfully",
	})
}
