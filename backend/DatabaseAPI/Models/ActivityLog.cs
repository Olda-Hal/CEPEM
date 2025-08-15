using System;
using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models
{
    public class ActivityLog
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        [MaxLength(100)]
        public string Service { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; }

        public string Details { get; set; }
    }
}
