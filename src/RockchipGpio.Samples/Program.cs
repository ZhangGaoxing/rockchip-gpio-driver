// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                if (item == -1)
                {
                    continue;
                }

                Console.WriteLine(item);
                int i = Array.IndexOf(NanoPiR2S._pinNumberConverter, item);

                controller.OpenPin(i);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(200);
                controller.SetPinMode(i, PinMode.InputPullUp);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(200);
                controller.SetPinMode(i, PinMode.InputPullDown);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(200);

                //controller.OpenPin(i, PinMode.Output);
                //controller.Write(i, 0);
                //Console.WriteLine(controller.Read(i));
                //Thread.Sleep(200);
                //controller.Write(i, 1);
                //Console.WriteLine(controller.Read(i));
                //Thread.Sleep(200);

                controller.ClosePin(i);
            }
        }
    }
}