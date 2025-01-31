using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FixtureManagementV3.Models
{
    public class Pitch
    {
        public Guid Id { get; set; }
        [Display(Name = "Pitch")]
        [JsonPropertyName("title")]
        public string Name { get; set; } = "";
        public int DisplayOrder { get; set; }
        ////Navigation properties
        //public List<TUFixture> Fixtures { get; set; }

    }
}

