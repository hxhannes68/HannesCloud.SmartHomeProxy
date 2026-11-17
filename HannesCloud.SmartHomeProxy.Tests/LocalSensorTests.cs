using HannesCloud.SmartHomeProxy.HomeAssistant.Models;
using HannesCloud.SmartHomeProxy.LocalSensors;
using Shouldly;

namespace HannesCloud.SmartHomeProxy.Tests;

public class LocalSensorReadingTests
{
    [Theory]
    [InlineData("pi1", 21.3, null)]
    [InlineData("Pi1", 21.3, "device")]
    [InlineData("pi 1", 21.3, "device")]
    [InlineData("", 21.3, "device")]
    [InlineData("pi1", -142.0, "temperature")]
    public void A_reading_names_its_device_plainly_and_stays_within_what_a_room_can_measure(
        string device, double temperature, string? problemAbout)
    {
        var problem = new LocalSensorReading { Device = device, Temperature = temperature }.Validate();

        if (problemAbout is null)
        {
            problem.ShouldBeNull();
            return;
        }

        problem.ShouldNotBeNull().ShouldStartWith(problemAbout);
    }

    [Fact]
    public void A_reading_without_any_value_is_turned_away()
    {
        new LocalSensorReading { Device = "pi1", Room = "Wohnzimmer" }.Validate().ShouldNotBeNull();
    }
}

public class LocalSensorMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Each_measurement_becomes_its_own_entity_named_after_the_device_and_carrying_the_room()
    {
        var states = LocalSensorMapper.ToStates(
            new LocalSensorReading { Device = "pi1", Room = " Wohnzimmer ", Temperature = 21.34, Humidity = 58.26, Pressure = 1013.44 }, Now);

        states.Select(s => s.EntityId).ShouldBe(["sensor.pi1_temperature", "sensor.pi1_humidity", "sensor.pi1_pressure"]);
        states.Select(s => s.State).ShouldBe(["21.3", "58.3", "1013.4"]);
        states.ShouldAllBe(s => s.Domain == "sensor" && (string?)s.Attributes["room"] == "Wohnzimmer" && (string?)s.Attributes["source"] == "pi");
        states[2].Attributes["device_class"].ShouldBe("atmospheric_pressure");
        states[0].FriendlyName.ShouldBe("pi1 Temperatur");
    }

    [Fact]
    public void Values_the_device_doesnt_measure_and_an_empty_room_are_left_out()
    {
        var states = LocalSensorMapper.ToStates(new LocalSensorReading { Device = "pi1", Room = "", Temperature = 18 }, Now);

        states.ShouldHaveSingleItem().Attributes.ContainsKey("room").ShouldBeFalse();
    }
}

public class LocalSensorThrottleTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => this.Now;
    }

    private static IReadOnlyList<EntityState> Reading(double temperature, string room = "Wohnzimmer") =>
        LocalSensorMapper.ToStates(new LocalSensorReading { Device = "pi1", Room = room, Temperature = temperature }, DateTimeOffset.UnixEpoch);

    private static int Send(LocalSensorThrottle throttle, IReadOnlyList<EntityState> states)
    {
        var due = throttle.Filter(states);
        throttle.MarkSent(due);
        return due.Count;
    }

    [Fact]
    public void Only_a_change_beyond_the_sensors_noise_goes_out_between_heartbeats()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        var throttle = new LocalSensorThrottle(clock);

        Send(throttle, Reading(21.30)).ShouldBe(1);
        clock.Now += TimeSpan.FromMinutes(1);
        Send(throttle, Reading(21.34)).ShouldBe(0);
        clock.Now += TimeSpan.FromMinutes(1);
        Send(throttle, Reading(21.46)).ShouldBe(1);
    }

    [Fact]
    public void A_new_room_goes_out_at_once_and_an_unchanged_value_at_least_every_five_minutes()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        var throttle = new LocalSensorThrottle(clock);

        Send(throttle, Reading(21.3)).ShouldBe(1);
        clock.Now += TimeSpan.FromMinutes(1);
        Send(throttle, Reading(21.3, room: "Schlafzimmer")).ShouldBe(1);

        clock.Now += TimeSpan.FromMinutes(4);
        Send(throttle, Reading(21.3, room: "Schlafzimmer")).ShouldBe(0);
        clock.Now += TimeSpan.FromMinutes(1);
        Send(throttle, Reading(21.3, room: "Schlafzimmer")).ShouldBe(1);
    }

    [Fact]
    public void A_reading_the_cloud_didnt_take_is_tried_again_next_time()
    {
        var throttle = new LocalSensorThrottle(new Clock(DateTimeOffset.UnixEpoch));

        throttle.Filter(Reading(21.3)).Count.ShouldBe(1);
        throttle.Filter(Reading(21.3)).Count.ShouldBe(1);
    }
}
