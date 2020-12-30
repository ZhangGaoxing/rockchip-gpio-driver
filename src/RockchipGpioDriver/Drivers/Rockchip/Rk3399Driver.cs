using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iot.Device.Gpio.Drivers
{
    public unsafe class Rk3399Driver : RockchipDriver
    {
        protected override uint[] GpioRegisterAddresses => 
            new[] { 0xFF73_0000, 0xFF73_0000, 0xFF78_0000, 0xFF78_8000, 0xFF79_0000 };
    }
}
