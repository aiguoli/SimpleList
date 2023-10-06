using Microsoft.VisualBasic;
using System;

namespace SimpleList.Models
{
	public class Link
    {
        public int ID { get; set; }
        public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public DateTime? DeletedAt { get; set; }
		public string title { get; set; }
		public string content { get; set; }
		public string password { get; set; }
		public DateTime expire_date { get; set; }
		public int user_id { get; set; }
		public int category_id { get; set; }
		public int views { get; set; }
    }

	public class LinkResponse
	{
		public int code { get; set; }
		public Link data { get; set; }
	}

	public class LinksResponse
	{
		public int code { get; set; }
		public Link[] data { get; set; }
	}

	public class CreateLinkResponse : LinkResponse
	{
		public string msg { get; set; }
	}
}
