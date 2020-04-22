using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElastiBuild.Commands;
using ElastiBuild.Extensions;
using Ionic.Zip;

namespace ElastiBuild.BullseyeTargets
{
    public class UnpackPackageTarget : BullseyeTargetBase<UnpackPackageTarget>
    {
        public static async Task RunAsync(BuildContext ctx)
        {
            await Task.Delay(0);
        }
    }
}
