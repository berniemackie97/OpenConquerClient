using System.Text;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginLoadingTimelineTests
{
    [Fact]
    public void ParsePreservesEveryFramePlacement()
    {
        LoginLoadingTimeline timeline = LoginLoadingTimeline.Parse(CreateTimeline());

        Assert.Equal((70d, -442.95d), timeline.PositionAt(18));
        Assert.Equal((264.05d, -248.9d), timeline.PositionAt(36));
        Assert.Equal((943.2d, 430.25d), timeline.PositionAt(99));
        Assert.Equal((954d, 441.05d), timeline.PositionAt(100));
    }

    [Fact]
    public void ParseRejectsMissingBarPlacement()
    {
        byte[] data = CreateTimeline(omittedFrame: 36);
        Assert.Throws<InvalidDataException>(() => LoginLoadingTimeline.Parse(data));
    }

    [Fact]
    public void ParseRejectsRepeatedBarPlacement()
    {
        byte[] data = CreateTimeline(repeatedFrame: 36);
        Assert.Throws<InvalidDataException>(() => LoginLoadingTimeline.Parse(data));
    }

    [Fact]
    public void PositionAtRejectsFramesBeforeTheBarOrPastTheClip()
    {
        LoginLoadingTimeline timeline = LoginLoadingTimeline.Parse(CreateTimeline());

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.PositionAt(17));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.PositionAt(101));
    }

    private static byte[] CreateTimeline(int? omittedFrame = null, int? repeatedFrame = null)
    {
        StringBuilder json = new("{\"394\":{\"frames\":100,\"events\":[");
        bool first = true;
        for (int frame = 18; frame <= 100; frame++)
        {
            double fillX = frame switch
            {
                18 => 70,
                36 => 264.05,
                99 => 943.2,
                100 => 954,
                _ => 70 + (frame - 18) * 10.7805,
            };
            double headX = frame switch
            {
                18 => -442.95,
                36 => -248.9,
                99 => 430.25,
                100 => 441.05,
                _ => -442.95 + (frame - 18) * 10.7805,
            };

            Append(frame, 4, fillX);
            if (frame != omittedFrame)
            {
                Append(frame, 9, headX);
            }

            if (frame == repeatedFrame)
            {
                Append(frame, 4, fillX);
            }
        }

        json.Append("]}}");
        return Encoding.UTF8.GetBytes(json.ToString());

        void Append(int frame, int depth, double x)
        {
            if (!first)
            {
                json.Append(',');
            }

            first = false;
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"[{frame},{{\"depth\":{depth},\"m\":[1,1,0,0,{x},0]}}]");
        }
    }
}
