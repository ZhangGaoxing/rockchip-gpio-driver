// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Device.Gpio;
using System.Threading.Tasks;
using Iot.Device.BoardLed;
using Iot.Device.Gpio.Drivers;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            using GpioController controller = new GpioController(PinNumberingScheme.Logical, new LuckFoxPicoDriver());

            int pin = 118;

            controller.OpenPin(pin, PinMode.Output);

            while (!Console.KeyAvailable)
            {
                controller.Write(pin, 1);
                Task.Delay(1000);
                controller.Write(pin, 0);
                Task.Delay(1000);
            }

            //for (int i = 0; i < LuckFoxPicoDriver._pinNumberConverter.Length; i++)
            //{
            //    if (LuckFoxPicoDriver._pinNumberConverter[i] != -1)
            //    {
            //        Console.WriteLine($"GPIO pin enabled for use: {i}.");

            //        controller.OpenPin(i);

            //        controller.SetPinMode(i, PinMode.Input);
            //        Console.WriteLine(controller.Read(i));
            //        controller.SetPinMode(i, PinMode.InputPullUp);
            //        Console.WriteLine(controller.Read(i));
            //        controller.SetPinMode(i, PinMode.InputPullDown);
            //        Console.WriteLine(controller.Read(i));

            //        controller.SetPinMode(i, PinMode.Output);
            //        controller.Write(i, 0);
            //        Console.WriteLine(controller.Read(i));
            //        controller.Write(i, 1);
            //        Console.WriteLine(controller.Read(i));

            //        controller.ClosePin(i);
            //    }
            //}

            //// Set debounce delay to 5ms
            //int debounceDelay = 50000;
            //int pin = 7;

            //Console.WriteLine($"Let's blink an on-board LED!");

            //using GpioController controller = new GpioController(PinNumberingScheme.Board, new OrangePi4Driver());
            //using BoardLed led = new BoardLed("status_led");

            //controller.OpenPin(pin, PinMode.InputPullUp);
            //led.Trigger = "none";
            //Console.WriteLine($"GPIO pin enabled for use: {pin}.");
            //Console.WriteLine("Press any key to exit.");

            //while (!Console.KeyAvailable)
            //{
            //    if (Debounce())
            //    {
            //        // Button is pressed
            //        led.Brightness = 1;
            //    }
            //    else
            //    {
            //        // Button is unpressed
            //        led.Brightness = 0;
            //    }
            //}

            //bool Debounce()
            //{
            //    long debounceTick = DateTime.Now.Ticks;
            //    PinValue buttonState = controller.Read(pin);

            //    do
            //    {
            //        PinValue currentState = controller.Read(pin);

            //        if (currentState != buttonState)
            //        {
            //            debounceTick = DateTime.Now.Ticks;
            //            buttonState = currentState;
            //        }
            //    }
            //    while (DateTime.Now.Ticks - debounceTick < debounceDelay);

            //    if (buttonState == PinValue.Low)
            //    {
            //        return true;
            //    }
            //    else
            //    {
            //        return false;
            //    }
            //}
        }
    }
}