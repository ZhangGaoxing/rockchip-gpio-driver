using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            int pin = RockchipDriver.MapPinNumber(4, 'C', 6);
            using GpioController controller = new GpioController(PinNumberingScheme.Logical, new Rk3399Driver());

            controller.OpenPin(pin, PinMode.InputPullUp);
            Console.WriteLine(controller.Read(pin));
            Console.WriteLine(controller.Read(pin));
            Console.Read();
        }
    }
}
