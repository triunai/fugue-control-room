using System.Text.Json;
using Fugue.Application.Common;
using Fugue.Domain.Customers;
using Fugue.Domain.Ids;

namespace Fugue.Tests.Domain;

public sealed class Uuid7Tests
{
    [Fact]
    public void Ids_are_version_7_and_strictly_increasing_even_within_one_millisecond()
    {
        byte[]? previous = null;
        for (int i = 0; i < 20_000; i++)
        {
            Guid id = Uuid7.NewGuid();
            byte[] bytes = id.ToByteArray(bigEndian: true);
            Assert.Equal(0x70, bytes[6] & 0xF0); // version 7
            Assert.Equal(0x80, bytes[8] & 0xC0); // RFC variant
            if (previous is not null)
            {
                Assert.True(previous.AsSpan().SequenceCompareTo(bytes) < 0, $"id {i} did not sort after its predecessor");
            }

            previous = bytes;
        }
    }

    [Fact]
    public void Embedded_timestamp_is_the_clock_time()
    {
        var at = new DateTimeOffset(2030, 6, 7, 8, 9, 10, 123, TimeSpan.Zero);

        Guid id = Uuid7.NewGuid(new FixedClock(at));

        Assert.Equal(at, Uuid7.Timestamp(id));
    }

    [Fact]
    public void Customer_id_parses_and_round_trips_json_and_rejects_garbage()
    {
        CustomerId id = CustomerId.New();

        Assert.True(CustomerId.TryParse(id.ToString(), null, out CustomerId parsed));
        Assert.Equal(id, parsed);
        Assert.Equal(id, JsonSerializer.Deserialize<CustomerId>(JsonSerializer.Serialize(id)));
        Assert.Equal($"\"{id}\"", JsonSerializer.Serialize(id));
        Assert.False(CustomerId.TryParse("garbage", null, out _));
        Assert.False(CustomerId.TryParse(Guid.Empty.ToString(), null, out _));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CustomerId>("\"garbage\""));
    }

    [Fact]
    public void Keyset_cursor_round_trips_and_rejects_garbage()
    {
        var cursor = new KeysetCursor(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), Guid.NewGuid());

        Assert.True(KeysetCursor.TryDecode(cursor.Encode(), out KeysetCursor decoded));
        Assert.Equal(cursor, decoded);
        Assert.DoesNotContain('=', cursor.Encode());
        Assert.False(KeysetCursor.TryDecode("garbage!", out _));
        Assert.False(KeysetCursor.TryDecode("", out _));
    }

    [Fact]
    public void Customer_transitions_are_guarded()
    {
        Customer customer = Customer.Create("A", "A@Example.Test", null);

        Assert.Equal("a@example.test", customer.Email);
        Assert.True(customer.Reactivate().IsFailed);
        Assert.True(customer.Suspend().IsSuccess);
        Assert.True(customer.Suspend().IsFailed);
        Assert.True(customer.Reactivate().IsSuccess);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
