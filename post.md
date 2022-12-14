# Indexing and Querying Embedded Objects with Redis OM .NET

> This post is my entry for this year's [C# Advent](https://csadvent.christmas/) - I'd encourage anyone reading this to look at this year's advent calendar and check out some of the great content that's been produced for it!

Before I get started - for those .NET devs interested learning Redis, I'd encourage you to sign up for my free course [Redis For .NET Developers](https://university.redis.com/courses/ru102n/), coming early next year - it'll be a deep dive into using Redis from the .NET eco-system.

Anyway let's get stared. For those of you who were here last year, you might remember my 2021 C# Advent post was [CRUD With Redis OM .NET](https://dev.to/slorello/crud-with-redis-om-net-c-advent-4gif) - the LINQ based Object Relation Mapper(ORM) (well really just OM since there's no relations in Redis:) ). It was a prescient moment to write that post given I had just released the first version of it days earlier. In this post, we'll walk through what I think is the most consequential new feature added to Redis OM .NET since I authored it in late 21' - the ability to index and query embedded objects within your documents in Redis.

## Prerequisites

* A .NET SDK that complies with .NET Standard 2.0 - I'm going to use the .NET 7 SDK, but you can easily adapt these examples to your workflow
* An IDE to work with C# from (I'll use Rider)
* Docker

## Spin up Docker

First things first, we'll need to spin up docker, you'll need to use an instance of [Redis Stack](https://redis.io/docs/stack/) for this, the easiest way to get one
up and running is to use:

```bash
docker run -d -p 6379:6379 redis/redis-stack-server
```

## Embedded Objects in Redis. . . Huh????

Hold-on, index and query? You might say. Redis is a key-value store what do you mean by that? Well, if you read through my C# Advent post from last year: [CRUD With Redis OM .NET](https://dev.to/slorello/crud-with-redis-om-net-c-advent-4gif) you'll see that RediSearch (which would be folded into Redis Stack which you are now running in docker) provides the ability to store, index, and query documents in Redis. And Redis OM .NET, the library I wrote to go along with Redis Stack's indexing and querying capabilities. So what's an embedded object? In this context, we mean that you have a complex object stored within a document. So if we have the following model:


```cs
public class Elf
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public Address HomeAddress { get; set; }
    public Address WorkAddress { get; set; }
}

public class Address
{
    public string StreetAddress { get; set; }
    public string PostalCode { get; set; }
    public GeoLoc Location { get; set; }
    public Address ForwardingAddress { get; set; }
}
```

In the initial version of Redis OM .NET, you would have been able to index all the simple scalars at the top level of `Elf`(`FirstName`, `LastName`, `Age`), but you would not be able to index and query `Elf.HomeAddress` and `Elf.WorkAddress`. However, as of v0.1.8, you can now index and query embedded documents in Redis. So let's get to it!

## Create our project

First things first, let's create our project and then cd into it, run the following in your terminal:

```
dotnet new console -n CS.Advent.TwentyTwo
cd CS.Advent.TwentyTwo
```

## Add Redis OM .NET to your project

Next, you just need to add Redis OM .NET to your project file, the simplest way to do this, since you're already in your terminal is to run:

```
dotnet add package Redis.OM
```

Now you can open CS.Advent.TwentyTwo in your IDE of choice. 

## Create our Model

Create a file Model.cs to it, and add the model from above to it. Now, if you followed along with my other post, you'll see that you can define a storage type for them (use JSON here) by using the `DocumentAttribute` - so for our Elf class we could use one like:

```cs
[Document(StorageType = StorageType.Json, IndexName = "Elves", Prefixes = new []{"Elf"})]
public class Elf
```

Since we aren't actually going to be storing addresses at the root level, we do not need to add a `DocumentAttribute` to our `Address` class.

### Index our Scalars

The next thing we need to do is to define the index on our scalars. This is straight forward, with the exception of `StreetAddress` add a `Indexed` attribute to each scalar (including the `GeoLoc`, but excluding the `Address` typed fields). For `StreetAddress` add the `Searchable` tag, and for the Id field add the `RedisIdField` attribute as well, when you're done the model should look like this:

```cs
[Document(StorageType = StorageType.Json, IndexName = "Elves", Prefixes = new []{"Elf"})]
public class Elf
{
    [RedisIdField] [Indexed] public string Id { get; set; }
    [Indexed] public string FirstName { get; set; }
    [Indexed] public string LastName { get; set; }
    [Indexed] public int Age { get; set; }
    public Address HomeAddress { get; set; }
    public Address WorkAddress { get; set; }
}

public class Address
{
    [Searchable] public string StreetAddress { get; set; }
    [Indexed] public string PostalCode { get; set; }
    [Indexed] public GeoLoc Location { get; set; }
    public Address ForwardingAddress { get; set; }
}
```

### Index our Embedded Objects

Now that we've indexed our scalars, we'll want to index our embedded objects. That's our `HomeAddress`, `WorkAddress`, and `ForwardingAddress`. There's two ways to do this

1. Provide the JSON Path to the parts of the document to Index.
2. Cascade into the document to a certain depth.

#### JSON Path Indexing

To index via JSON path, add a JSON path in your `IndexAttribute` defining exactly what you want to index. So if you wanted to index only the `PostalCode` and `Location` of the `WorkAddress` you would just need to add the JSON path to them in a series of `IndexedAttribute`s.

```cs
[Indexed(JsonPath = "$.PostalCode")]
[Indexed(JsonPath = "$Location")]
public Address WorkAddress { get; set; }
```

#### Cascading Index

If you want to cascade into the object and index everything underneath it as defined, you just need to set a `CascadeDepth`. When the `CascadeDepth` property is specified in the `Indexed` attribute, Redis OM will recursively search the object tree down to the specified depth and index everything underneath it. So if we wanted to pick up our `HomeAddress.ForwardingAddress` you would set the `ForwardingAddress`'s cascade depth to 1:

```cs
[Indexed(CascadeDepth = 1)]
public Address ForwardingAddress { get; set; }
```

And the `HomeAddress`'s cascade depth to 2:

```cs
[Indexed(CascadeDepth = 2)]
public Address HomeAddress { get; set; }
```

## Create our Index

Let's switch gears and actually create our index in Redis. All we need to do to do that is connect our `RedisConnectionProvider` and call `IRedisConnection.CreateIndex`, switch over to our `Program.cs` file and add the following:

```cs
using CS.Advent.TwentyTwo;
using Redis.OM;

var provider = new RedisConnectionProvider("redis://localhost:6379");
provider.Connection.CreateIndex(typeof(Elf));
```

## Add our Elves

Let's add a couple elves from Christmas cannon into Redis. We'll use Buddy from Elf, and Bernard from the Santa Clause - an amusing constraint for Redis is that it cannot index anything above the 85th parallel, so we'll put Bernard in Alaska, and Buddy in NY:


```cs
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
```

## Query our Elves

Now all we need to do is try querying our elves. If you wanted to query all the elves at the postal code `99705`, all you'd need to do is run the query:

```cs
var elvesAt99705 = elves.Where(x=>x.WorkAddress.PostalCode == "99705");

foreach (var elf in elvesAt99705)
{
    Console.WriteLine($"{elf.FirstName} works in the 99705 postal code");
}
```

Likewise if you want to query all the Elves near Macy's you can do so bu running a geo-filter against the Location property:

```cs
var elvesNearMacys = elves.GeoFilter(x=>x.HomeAddress.Location, -73.991,40.750,2, GeoLocDistanceUnit.Miles);

foreach (var elf in elvesNearMacys)
{
    Console.WriteLine($"{elf.FirstName} is near Macy's");
}
```

## Run our Code

All that's left to do is run our code, if you run:

```bash
dotnet run
```

from the terminal, everything will execute, and you'll see that Bernard is in fact in Alaska, and Buddy is in NY!

## Wrapping up

Thanks for stopping by the C# Advent, hopefully you found this useful. Here's some more resources for you if you're interested in Redis, Redis Stack, and Redis OM:

1. The Best place to learn about Redis OM is currently it's [README in GitHub](https://github.com/redis/redis-om-dotnet)
2. There's also a tutorial for Redis OM .NET on [Redis Developer](https://developer.redis.com/develop/dotnet/redis-om-dotnet/getting-started)
3. As I mentioned at the top, I am putting together a course- [Redis for .NET Developers](https://university.redis.com/courses/ru102n/) which is free and will be available early next year
4. If you're interested in my work - please follow me on [Twitter](https://twitter.com/slorello) or [GitHub](https://github.com/slorello89)