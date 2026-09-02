package handler

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/gofiber/fiber/v2"
)

func Collect(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{"code": 401, "msg": "Unauthorized"})
	}
	var link models.Link
	if err := database.DB.First(&link, c.Params("id")).Error; err != nil {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{"code": 404, "msg": "Link not found"})
	}
	collection := models.Collection{UserID: userID, ShareID: link.ID}
	if err := database.DB.Where("user_id = ? AND share_id = ?", userID, link.ID).FirstOrCreate(&collection).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{"code": 500, "msg": "Couldn't collect link"})
	}
	return c.JSON(fiber.Map{"code": 200, "msg": "Link collected successfully"})
}

func Uncollect(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{"code": 401, "msg": "Unauthorized"})
	}
	if err := database.DB.Where("user_id = ? AND share_id = ?", userID, c.Params("id")).Delete(&models.Collection{}).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{"code": 500, "msg": "Couldn't uncollect link"})
	}
	return c.JSON(fiber.Map{"code": 200, "msg": "Link uncollected successfully"})
}

func Collections(c *fiber.Ctx) error {
	userID, ok := currentUserID(c)
	if !ok {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{"code": 401, "msg": "Unauthorized"})
	}
	var links []models.Link
	if err := database.DB.Model(&models.Link{}).
		Joins("JOIN collections ON collections.share_id = links.id AND collections.deleted_at IS NULL").
		Where("collections.user_id = ?", userID).
		Order("collections.created_at DESC").
		Find(&links).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{"code": 500, "msg": "Couldn't get collections"})
	}
	return c.JSON(fiber.Map{"code": 200, "data": links})
}
