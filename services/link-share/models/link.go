package models

import (
	"gorm.io/gorm"
	"time"
)

type Link struct {
	gorm.Model
	Title          string    `json:"title"`
	Content        string    `json:"content"`
	Password       string    `json:"password"`
	ExpirationDate time.Time `json:"expire_date"`
	UserID         uint      `json:"user_id"`
	CategoryID     uint      `json:"category_id"`
	ProviderType   string    `gorm:"not null;default:onedrive;index" json:"provider_type"`
	Views          uint      `json:"views"`
}
