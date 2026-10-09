namespace FRONTEND.Data
{
    // Stored as an embedded array inside its Album document in Cosmos DB.
    public class Track
    {
        public string? Name { get; set; }
        public float Length { get; set; }
    }
}
