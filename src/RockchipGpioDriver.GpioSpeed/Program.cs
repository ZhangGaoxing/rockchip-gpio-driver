﻿using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;

namespace RockchipGpioDriver.GpioSpeed
{
    class Program
    {
        static void Main(string[] args)
        {
            int pin = 150;
            GpioController controller;

            Console.WriteLine("Select GPIO driver: ");
            Console.WriteLine("1. SysFsDriver; 2. LibGpiodDriver; 3. RockchipDriver");

            string key = Console.ReadLine();
            switch (key)
            {
                case "1":
                    controller = new GpioController(PinNumberingScheme.Logical, new SysFsDriver());
                    break;
                case "2":
                    controller = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver());
                    break;
                case "3":
                    controller = new GpioController(PinNumberingScheme.Logical, new OrangePi4Driver());
                    break;
                default:
                    Console.WriteLine("Exit");
                    Environment.Exit(0);
                    return;
            }

            using (controller)
            {
                controller.OpenPin(pin, PinMode.Output);
                Console.WriteLine("Press any key to exit.");

                while (!Console.KeyAvailable)
                {
                    controller.Write(pin, PinValue.High);
                    controller.Write(pin, PinValue.Low);
                }
            }
        }
    }
}
