using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;
using System.Threading;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            using GpioController controller = new GpioController(PinNumberingScheme.Board, new OrangePi4());

            for (int i = 1; i <= 40; i++)
            {
                Console.WriteLine(i);
                if (OrangePi4._pinNumberConverter[i] == -1)
                {
                    continue;
                }

                controller.OpenPin(i, PinMode.InputPullUp);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);
                controller.SetPinMode(i, PinMode.InputPullDown);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);
                controller.SetPinMode(i, PinMode.Input);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);
                controller.ClosePin(i);
            }
        }
    }
}
