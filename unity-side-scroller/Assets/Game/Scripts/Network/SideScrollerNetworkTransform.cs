using Mirror;

namespace EasyGame.SideScroller.Network
{
    public sealed class SideScrollerNetworkTransform : NetworkTransformReliable
    {
        public override void Reset()
        {
            target ??= transform;
            base.Reset();
        }

        public override void ResetState()
        {
            target ??= transform;
            base.ResetState();
        }
    }
}
