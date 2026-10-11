using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FRONTEND.Services;


namespace FRONTEND.Data
{
    public class Album
    {
        #region PROPS
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required(ErrorMessage = "Album name is required.")]
        public string? Name { get; set; }
        
        [Required(ErrorMessage = "Artist is required.")]
        public string? Artist {get; set;}
        [JsonIgnore] // Don't serialize to JSON, calculate at runtime
        public string? PrimaryColour { get; set; }
        
        [Range(0, 5, ErrorMessage = "Rating must be between 0 and 5.")]
        public int Rating { get; set; }
        public int Release { get; set; }
        public float Length { get; set; }
        public string? ImgSrc { get; set; }
        public string? ImgSrcBack { get; set; }
        public string? BannerImg { get; set; }

        // Sort key for the Ranking page, lowest first. Values are spaced out (10, 20, 30...)
        // so an album can be slotted between two others by giving it a value in the gap,
        // e.g. 125 between 120 and 130, without renumbering the rest. Null = unranked.
        public double? Ranking { get; set; }

        public string? BestTrack { get; set; }
        public string? SpotifyLink { get; set; }
        public string? SpotifyiFrame { get; set; }
        public List<Track>? TrackList { get; set; }
        #endregion
    
        #region METHODS

        public async Task<string> CalculatePrimaryColourAsync(IColorAnalysisService colorService)
        {
            if (string.IsNullOrEmpty(PrimaryColour) && !string.IsNullOrEmpty(ImgSrc))
            {
                PrimaryColour = await colorService.GetDominantColorAsync(ImgSrc);
            }
            return PrimaryColour ?? "#000000";
        }
        #endregion
    }
}
