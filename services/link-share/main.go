package main

import (
	"github.com/aiguoli/SimpleList/services/link-share/database"
	appMiddleware "github.com/aiguoli/SimpleList/services/link-share/middleware"
	"github.com/aiguoli/SimpleList/services/link-share/routes"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/middleware/cors"
	"github.com/gofiber/fiber/v2/middleware/logger"
	"github.com/joho/godotenv"
	"log"
	"os"
	"strings"
)

func main() {
	if err := godotenv.Load(); err != nil {
		log.Println(".env not found, using system environment variables")
	}
	if err := appMiddleware.ValidateConfiguration(); err != nil {
		log.Fatal(err)
	}
	if err := database.ConnectDB(); err != nil {
		log.Fatal(err)
	}

	app := fiber.New()

	file, _ := os.OpenFile("./link-share.log", os.O_RDWR|os.O_CREATE|os.O_APPEND, 0666)
	defer func(file *os.File) {
		err := file.Close()
		if err != nil {
			return
		}
	}(file)
	app.Use(logger.New(logger.Config{
		Format:     "[${time}][${ip}] ${status} - ${method} ${path}\n",
		TimeFormat: "2006-01-02 15:04:05",
		Output:     file,
	}))
	allowedOrigins := strings.TrimSpace(os.Getenv("CORS_ALLOW_ORIGINS"))
	if allowedOrigins == "" {
		allowedOrigins = "https://share.qqsign.cn"
	}
	app.Use(cors.New(cors.Config{AllowOrigins: allowedOrigins, AllowHeaders: "Origin, Content-Type, Accept, Authorization"}))

	app.Get("/", func(c *fiber.Ctx) error {
		return c.SendString("Working!")
	})
	app.Get("/health/live", func(c *fiber.Ctx) error { return c.SendStatus(fiber.StatusOK) })
	app.Get("/health/ready", func(c *fiber.Ctx) error {
		sqlDB, err := database.DB.DB()
		if err != nil || sqlDB.Ping() != nil {
			return c.SendStatus(fiber.StatusServiceUnavailable)
		}
		return c.SendStatus(fiber.StatusOK)
	})
	routes.SetupRoutes(app)
	err := app.Listen(":3000")
	if err != nil {
		return
	}
}
