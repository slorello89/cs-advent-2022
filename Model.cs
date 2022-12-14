using Redis.OM.Modeling;

namespace CS.Advent.TwentyTwo;

[Document(StorageType = StorageType.Json, IndexName = "Elves", Prefixes = new []{"Elf"})]
public class Elf
{
    [RedisIdField] [Indexed] public string Id { get; set; }
    [Indexed] public string FirstName { get; set; }
    [Indexed] public string LastName { get; set; }
    [Indexed] public int Age { get; set; }
    [Indexed(CascadeDepth = 2)]
    public Address HomeAddress { get; set; }
    [Indexed(JsonPath = "$.PostalCode")]
    [Indexed(JsonPath = "$Location")]
    public Address WorkAddress { get; set; }
}

public class Address
{
    [Searchable] public string StreetAddress { get; set; }
    [Indexed] public string PostalCode { get; set; }
    [Indexed] public GeoLoc Location { get; set; }
    [Indexed(CascadeDepth = 1)]
    public Address ForwardingAddress { get; set; }
}