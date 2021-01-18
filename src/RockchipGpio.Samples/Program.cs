using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Threading;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            using GpioController controller = new GpioController(PinNumberingScheme.Board, new OrangePi4());

            //foreach (var item in OrangePi4._pinNumberConverter)
            //{
            //    if (item == -1)
            //    {
            //        continue;
            //    }

            //    Console.WriteLine(item);
            //    int i = Array.IndexOf(OrangePi4._pinNumberConverter, item);

            //    controller.OpenPin(i);
            //    Console.WriteLine(controller.Read(i));
            //    Thread.Sleep(200);
            //    controller.SetPinMode(i, PinMode.InputPullUp);
            //    Console.WriteLine(controller.Read(i));
            //    Thread.Sleep(200);
            //    controller.SetPinMode(i, PinMode.InputPullDown);
            //    Console.WriteLine(controller.Read(i));
            //    Thread.Sleep(200);

            //    //controller.OpenPin(i, PinMode.Output);
            //    //controller.Write(i, 0);
            //    //Thread.Sleep(500);
            //    //Console.WriteLine(controller.Read(i));
            //    //Thread.Sleep(500);
            //    //controller.Write(i, 1);
            //    //Console.WriteLine(controller.Read(i));

            //    controller.ClosePin(i);
            //}

            int pin = 3;
            controller.OpenPin(pin, PinMode.Input);
            while (!Console.KeyAvailable)
            {
                Console.WriteLine(controller.Read(pin));
                Thread.Sleep(500);
            }
        }
    }
}