using ATENtion.Core.Net;
using Xunit;

namespace ATENtion.Tests
{
    /// <summary>Locks down the video request-cadence defaults.</summary>
    public class VideoSessionDefaultsTests
    {
        [Fact]
        public void Defaults_To_Pipelined_Requests_Without_Periodic_Full_Refresh()
        {
            using (var session = new KvmVideoSession(new KvmConnectionOptions()))
            {
                // A forced full frame is the most expensive request the session can make, and at a
                // periodic interval it dominates the video budget. Stale tiles are repaired on
                // demand through View > Refresh instead.
                Assert.Equal(0, session.FullRefreshIntervalTicks);

                // Two requests in flight let the BMC encode the next frame while the current one is
                // decoded, rather than paying a full round trip per frame.
                Assert.Equal(2, session.PipelineDepth);
            }
        }
    }
}
