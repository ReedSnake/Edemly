namespace Edemly.Server.Data.Entities
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // Physical database name for the tenant
        public string DbName { get; set; } = string.Empty;
    }
}
