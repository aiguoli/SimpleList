package routes

import (
	"github.com/aiguoli/SimpleList/services/link-share/handler"
	"github.com/aiguoli/SimpleList/services/link-share/middleware"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/middleware/limiter"
	"github.com/gofiber/fiber/v2/middleware/logger"
	"time"
)

func SetupRoutes(app *fiber.App) {
	// Middleware
	api := app.Group("/api", logger.New())

	// Auth
	auth := api.Group("/auth")
	auth.Post("/register", handler.Register)
	auth.Post("/login", handler.Login)

	// User
	user := api.Group("/users")
	user.Get("/:id", handler.GetUser)
	user.Patch("/:id", middleware.Protected(), handler.UpdateUser)

	// Link
	link := api.Group("/links")
	link.Get("/", handler.GetLinks)
	link.Get("/:id", handler.GetLink)
	link.Post("/", middleware.Protected(), handler.CreateLink)
	link.Post("/:id/visit", middleware.Protected(), handler.IncrementViews)
	link.Patch("/:id", middleware.Protected(), handler.UpdateLink)
	link.Delete("/:id", middleware.Protected(), handler.DeleteLink)

	// Collection
	collection := api.Group("/")
	collection.Post("/collect/:id", middleware.Protected(), handler.Collect)
	collection.Post("/uncollect/:id", middleware.Protected(), handler.Uncollect)
	collection.Get("/collections", middleware.Protected(), handler.Collections)

	// Category
	category := api.Group("/categories")
	category.Get("/", handler.GetCategories)
	category.Post("/", middleware.Protected(), handler.CreateCategory)
	category.Patch("/:id", middleware.Protected(), handler.UpdateCategory)
	category.Delete("/:id", middleware.Protected(), handler.DeleteCategory)

	// Provider-neutral v2 API. v1 routes remain available for older clients.
	v2 := api.Group("/v2")
	authLimiter := limiter.New(limiter.Config{Max: 10, Expiration: time.Minute})
	v2Auth := v2.Group("/auth", authLimiter)
	v2Auth.Post("/register", handler.RegisterV2)
	v2Auth.Post("/login", handler.LoginV2)
	v2Auth.Post("/refresh", handler.RefreshV2)
	v2Auth.Post("/logout", handler.LogoutV2)
	v2.Get("/users/me", middleware.Protected(), middleware.RequireAccessToken, handler.MeV2)
	v2.Get("/providers", handler.GetProvidersV2)
	v2.Get("/categories", handler.GetCategories)
	v2.Get("/collections", middleware.Protected(), middleware.RequireAccessToken, handler.CollectionsV2)
	v2Links := v2.Group("/links")
	v2Links.Get("/", handler.GetLinksV2)
	v2Links.Get("/:id", handler.GetLinkV2)
	v2Links.Post("/", middleware.Protected(), middleware.RequireAccessToken, handler.CreateLinkV2)
	v2Links.Post("/:id/visit", handler.IncrementViewsV2)
	v2Links.Put("/:id/collection", middleware.Protected(), middleware.RequireAccessToken, handler.CollectV2)
	v2Links.Delete("/:id/collection", middleware.Protected(), middleware.RequireAccessToken, handler.UncollectV2)
	v2Links.Patch("/:id", middleware.Protected(), middleware.RequireAccessToken, handler.UpdateLinkV2)
	v2Links.Delete("/:id", middleware.Protected(), middleware.RequireAccessToken, handler.DeleteLinkV2)
}
