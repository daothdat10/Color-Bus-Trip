using NUnit.Framework;

public sealed class LevelDataLoadTests
{
    [Test]
    public void MissingBusPositionUsesToolCenteredGridLayout()
    {
        const string json =
            "{\"level\":{\"id\":\"Level_01\",\"boardData\":{" +
            "\"grid\":{\"width\":2,\"height\":1},\"items\":[" +
            "{\"type\":\"Bus\",\"typeId\":3,\"data\":{\"column\":0,\"row\":0,\"passengerType\":1}}," +
            "{\"type\":\"Bus\",\"typeId\":3,\"data\":{\"column\":1,\"row\":0,\"passengerType\":1}}]}}}";

        LevelData data = LevelData.FromJson(json);

        Assert.That(data.buses.Count, Is.EqualTo(2));
        Assert.That(data.buses[0].column, Is.EqualTo(0));
        Assert.That(data.buses[0].row, Is.EqualTo(0));
        Assert.That(data.buses[1].column, Is.EqualTo(1));
        Assert.That(data.buses[1].row, Is.EqualTo(0));
        Assert.That(data.buses[0].posX, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(data.buses[1].posX, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(data.buses[0].posZ, Is.EqualTo(-8.6f).Within(0.0001f));
        Assert.That(data.buses[1].posZ, Is.EqualTo(-10.92f).Within(0.0001f));
    }
}
