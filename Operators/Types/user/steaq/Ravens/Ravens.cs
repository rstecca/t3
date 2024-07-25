using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_390f36e8_9bcd_4420_9dd9_81b450834a53
{
    public class Ravens : Instance<Ravens>
    {
        [Output(Guid = "fc5d1332-98f6-4d54-b8ef-177e87156766")]
        public readonly Slot<Texture2D> ImgOutput = new();


    }
}

