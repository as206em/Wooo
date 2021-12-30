//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Diagnostics;
//using System.IO;
//using System.IO.Ports;
//using System.Linq;
//using System.Net;
//using System.Threading;
//using System.Windows.Forms;
//using System.Web;
//using Corale.Colore.Core;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Web;

namespace WoowShortCuts
{
    class MyKeyboard
    {
        Corale.Colore.Core.Color[][] BlackWidowColors;

        Corale.Colore.Core.Color[] FireflyColors;
        Corale.Colore.Razer.Mousepad.Effects.Custom FireFlyCustomEffect;
        Corale.Colore.Core.Color PressedbuttonColor = new Corale.Colore.Core.Color(255, 255, 255);
        int R1, G1, B1;
        int fadeInfactor = 5;

        //default constructor
        public MyKeyboard()
        {
            try
            {
                BlackWidowColors = new Corale.Colore.Core.Color[6][];
                for (int i = 0; i < BlackWidowColors.Length; i++)
                {
                    BlackWidowColors[i] = new Corale.Colore.Core.Color[22];
                }
            }
            catch { }

        }

        //fill the keyboard with the first static color
        public void FillUpStatic()
        {
            try
            {
                for (int i = 0; i < BlackWidowColors.Length; i++)
                {
                    for (int j = 0; j < 22; j++)
                    {
                        hueToRGB((int)(j * 7.727272727272727), 255);
                        BlackWidowColors[i][j] = Corale.Colore.Core.Color.FromRgb(ColorToUInt(System.Drawing.Color.FromArgb(R1, G1, B1)));

                        Corale.Colore.Core.Keyboard.Instance.SetPosition(i, j, BlackWidowColors[i][j]);
                    }
                }

            }
            catch { }


            //setup mousepad
            try
            {
                FireflyColors = new Corale.Colore.Core.Color[15];

                for (int i = 0; i < FireflyColors.Length; i++)
                {
                    hueToRGB((int)(i * 17), 255);
                    FireflyColors[i] = Corale.Colore.Core.Color.FromRgb(ColorToUInt(System.Drawing.Color.FromArgb(R1, G1, B1)));
                }

                FireFlyCustomEffect = new Corale.Colore.Razer.Mousepad.Effects.Custom(FireflyColors.ToList());

                Corale.Colore.Core.Mousepad.Instance.SetCustom(FireFlyCustomEffect);
            }
            catch { }


        }

        /*      some private functions*/

        //get uInt from Color
        private uint ColorToUInt(System.Drawing.Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | (color.B << 0));
        }


        //convert value from hue to RGB,all in bytes, 255 max
        private void hueToRGB(int hue, int brightness)
        {
            ushort prev = (ushort)((brightness * (255 - ((ushort)(((ushort)(hue * 6)) - (((ushort)(((ushort)(hue * 6)) / 256)) * 256))))) / 256);
            ushort next = (ushort)((brightness * ((ushort)(((ushort)(hue * 6)) - (((ushort)(((ushort)(hue * 6)) / 256)) * 256)))) / 256);
            switch (((ushort)(((ushort)(hue * 6)) / 256)))
            {
                case 0:      // red
                    R1 = brightness;
                    G1 = next;
                    B1 = 0;
                    break;
                case 1:     // yellow
                    R1 = prev;
                    G1 = brightness;
                    B1 = 0;
                    break;
                case 2:     // green
                    R1 = 0;
                    G1 = brightness;
                    B1 = next;
                    break;
                case 3:    // cyan
                    R1 = 0;
                    G1 = prev;
                    B1 = brightness;
                    break;
                case 4:    // blue
                    R1 = next;
                    G1 = 0;
                    B1 = brightness;
                    break;
                case 5:      // magenta
                default:
                    R1 = brightness;
                    G1 = 0;
                    B1 = prev;
                    break;
            }
        }

    }
}
