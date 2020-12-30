using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            using GpioController controller = new GpioController(PinNumberingScheme.Logical, new Rk3399Driver());
            controller.OpenPin(157, PinMode.Input);
            Console.WriteLine(controller.Read(157));
            Console.Read();
        }
    }
}
