using CS.Advent.TwentyTwo;
using Redis.OM;
using Redis.OM.Modeling;

var provider = new RedisConnectionProvider("redis://localhost:6379");
provider.Connection.Execute("FLUSHDB");
provider.Connection.CreateIndex(typeof(Elf));

var buddy = new Elf
{
    FirstName = "Buddy",
    LastName = "Hobbs",
    Age = 30,
    HomeAddress = new Address { StreetAddress = "55 Central Park West", PostalCode = "10023", Location = new GeoLoc(-73.979,40.772) },
    WorkAddress = new Address { StreetAddress = "119 West 31st Street", PostalCode = "10001", Location = new GeoLoc(-73.991,40.748) }
};

var bernard = new Elf
{
    FirstName = "Bernard",
    LastName = "The Arch Elf",
    Age = 1530,
    HomeAddress = new Address{StreetAddress = "101 St Nicholas Dr", PostalCode = "99705",Location = new GeoLoc(-147.343, 64.755)},
    WorkAddress = new Address{StreetAddress = "101 St Nicholas Dr", PostalCode = "99705",Location = new GeoLoc(-147.343, 64.755)}
};

var elves = provider.RedisCollection<Elf>();

elves.Insert(buddy);
elves.Insert(bernard);

var elvesAt99705 = elves.Where(x=>x.WorkAddress.PostalCode == "99705");

foreach (var elf in elvesAt99705)
{
    Console.WriteLine($"{elf.FirstName} works in the 99705 postal code");
}

var elvesNearMacys = elves.GeoFilter(x=>x.HomeAddress.Location, -73.991,40.750,2, GeoLocDistanceUnit.Miles);

foreach (var elf in elvesNearMacys)
{
    Console.WriteLine($"{elf.FirstName} is near Macy's");
}