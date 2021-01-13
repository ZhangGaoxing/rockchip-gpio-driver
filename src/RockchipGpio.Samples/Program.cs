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
            using GpioController controller = new GpioController(PinNumberingScheme.Board, new NanoPiR2S());

            foreach (var item in NanoPiR2S._pinNumberConverter)
            {
                Console.WriteLine(item);
                if (item == -1)
                {
                    continue;
                }

                int i = Array.IndexOf(NanoPiR2S._pinNumberConverter, item);

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