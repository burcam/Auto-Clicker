﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker
{
    public partial class AutoClicker : Form
    {
        //These are to be set whenever the mouse position is moved by the user. 
        private int centerMouseX;
        private int centerMouseY;

        private bool run = false;
        private int minTimeDefault = 1600;
        private int maxTimeDefault = 2600;
        private int minClickBeforeMouseMoveDefault = 1;
        private int maxClickBeforeMouseMoveDefault = 30;
        private int mousePixelsToMoveFromCenter = 3;

        private int mouseClicksThisRun = 0;

        private static object threadLocker = new object();

        delegate void UpdateMouseCoordLabelsCallback(string xCoord, string yCoord);

        /// <summary>
        /// Random number generator
        /// </summary>
        private Random random = new Random();

        public AutoClicker()
        {
            InitializeComponent();
            InitializeWaitTimes();

            //Mouse coordinate label thread
            Thread mouseCoordinateLabelThread = new Thread(() => ContinuallyUpdateMouseCoordLabel());
            mouseCoordinateLabelThread.IsBackground = true;
            mouseCoordinateLabelThread.Start();
        }

        private void ContinuallyUpdateMouseCoordLabel(int precisionMilliseconds = 60)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(precisionMilliseconds); //Update every 60 milliseconds

                UpdateMouseCoordLabelsCallback updateMouseCoordInvoker = new UpdateMouseCoordLabelsCallback(SetMouseCoordLabels);

                string currentXCoord = Cursor.Position.X.ToString();
                string currentYCoord = Cursor.Position.Y.ToString();

                try
                {
                    Invoke(updateMouseCoordInvoker, new object[] { currentXCoord, currentYCoord });
                }
                catch (Exception) { };
            }
        }

        /// <summary>
        /// Set the min/max wait time and mouse movement clicks to default values. 
        /// Min wait time indicates the minimum amount of time waited before the next click. 
        /// Max wait time indicates the maximum amount of time waitied before the next click.
        /// minClickBeforeMouseMove indicates the minimum number of clicks needed before mouse jumps position.
        /// maxClicksBeforeMouseMove indicates the maximum number of clicks needed before mouse jumps position.
        /// </summary>
        private void InitializeWaitTimes()
        {
            minWait.Text = minTimeDefault.ToString();
            maxWait.Text = maxTimeDefault.ToString();
            minClicksBetweenMovement.Text = minClickBeforeMouseMoveDefault.ToString();
            maxClicksBetweenMovement.Text = maxClickBeforeMouseMoveDefault.ToString();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            StartAutoClicker();
        }

        /// <summary>
        /// This is a gatekeeper method to keep only a single auto-clicker thread running 
        /// </summary>
        private void StartAutoClicker()
        {
            String errorType;

            if (ValidFieldData(out errorType))
            {
                if (!run)
                    AutoClickOnNewThread();
                run = true;
                DisableSettingFields();
            }
            else
            {
                string errorMessage;
                string caption;

                if (errorType == "NON-INT")
                {
                    errorMessage = "Wait Time and Mouse Clicks need to be non-decimal numbers!";
                    caption = "Invalid input(s)";
                }
                else
                {
                    errorMessage = "Max clicks/Max time cannot be less than Min clicks/Min time!";
                    caption = "Max < Min Error";
                }

                MessageBox.Show(errorMessage, caption, MessageBoxButtons.OK);

            }
        }

        /// <summary>
        /// Verifies that minClicksBetweenMovement, maxClicksBetweenMovement, minWait, and maxWait have integer values in them. 
        /// </summary>
        /// <returns></returns>
        private bool ValidFieldData(out string typeError)
        {
            typeError = "NONE";

            if (!(
                int.TryParse(minClicksBetweenMovement.Text, out int minClicks) &&
                int.TryParse(maxClicksBetweenMovement.Text, out int maxClicks) &&
                int.TryParse(this.minWait.Text, out int minWait) &&
                int.TryParse(this.maxWait.Text, out int maxWait)
                ))
            {
                typeError = "NON-INT";
                return false;
            }

            if (maxClicks < minClicks || maxWait < minWait)
            {
                typeError = "MAX LESS THAN MIN";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a new background thread and runs AutoClick() on that thread. 
        /// </summary>
        private void AutoClickOnNewThread()
        {
            centerMouseX = Cursor.Position.X;
            centerMouseY = Cursor.Position.Y;

            Thread t = new Thread(AutoClick);
            t.IsBackground = true;
            t.Start();
        }
        private void AutoClick()
        {

            int minWaitTime = int.Parse(minWait.Text);
            int maxWaitTime = int.Parse(maxWait.Text);

            int minClicks = int.Parse(minClicksBetweenMovement.Text);
            int maxClicks = int.Parse(maxClicksBetweenMovement.Text);

            while (run)
            {
                int numbersToClickBeforeUpdatingPosition = random.Next(minClicks, maxClicks); //Number of times to click before changing mouse position
                int counter = 0;

                //Number of clicks before randomly moving mouse position.
                while (counter < numbersToClickBeforeUpdatingPosition && run)
                {
                    int timeBetweenClicks = random.Next(minWaitTime, maxWaitTime);
                    DoMouseClick();
                    mouseClicksThisRun++; //Keep track of mouse clicks since user started clicker. 

                    MethodInvoker numClicksLabelUpdater = new MethodInvoker(() => SetMouseClickLabel(mouseClicksThisRun));
                    Invoke(numClicksLabelUpdater);

                    Thread.Sleep(timeBetweenClicks);
                    counter++;
                }

                //Also need an auto clicking thing.
                MethodInvoker mouseCoordUpdateInvoker = new MethodInvoker(RandomlyUpdateMouseCoordinates);
                Invoke(mouseCoordUpdateInvoker);

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// The point of the random mouse movements is to not click the exact same coordinate every click. 
        /// If there was a game, for example, and you wanted to hide the fact that you were using an auto-clicker
        /// Moving the mouse coordinates just a little bit every so often could help mask this fact.
        /// </summary>
        private void RandomlyUpdateMouseCoordinates()
        {

            if (run)
            {
                UpdateMouseCenterIfUserMovedMouse();

                int randX = centerMouseX + random.Next(1, mousePixelsToMoveFromCenter);
                int randY = centerMouseY + random.Next(1, mousePixelsToMoveFromCenter);

                Win32.POINT p = new Win32.POINT();

                int sleepTime = random.Next(1000, 4500); //Sleep between 1 and 4.5 seconds..?

                Win32.ClientToScreen(Handle, ref p);
                Win32.SetCursorPos(randX, randY);
                System.Threading.Thread.Sleep(sleepTime);

                SetMouseCoordLabels(Cursor.Position.X.ToString(), Cursor.Position.Y.ToString());

            }
        }

        /// <summary>
        /// Updates the form's Mouse X and Y Label with the new string values 
        /// for x and y respectively
        /// </summary>
        /// <param name="xPos"></param>
        /// <param name="yPos"></param>
        private void SetMouseCoordLabels(string xPos, string yPos)
        {
            //this.MouseXLabel.Text = "Mouse X Position: " + xPos;
            //this.MouseYLabel.Text = "Mouse Y position: " + yPos;
        }

        /// <summary>
        /// Simulates a click at the cursor's current location
        /// </summary>
        private void DoMouseClick()
        {
            if (run)
            {
                //Call the imported function with the cursor's current position
                uint X = (uint)Cursor.Position.X;
                uint Y = (uint)Cursor.Position.Y;
                Win32.mouse_event(Win32.MOUSEEVENTF_LEFTDOWN | Win32.MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
            }
        }

        /// <summary>
        /// Keeps track of the new mouse center to randomly move around.
        /// This allows the user to move the mouse and have a new center 
        /// </summary>
        private void UpdateMouseCenterIfUserMovedMouse()
        {
            if (Math.Abs(Cursor.Position.X - centerMouseX) > mousePixelsToMoveFromCenter
                || Math.Abs(Cursor.Position.Y - centerMouseY) > mousePixelsToMoveFromCenter)
            {
                centerMouseX = Cursor.Position.X;
                centerMouseY = Cursor.Position.Y;
            }
        }

        public class Win32
        {
            [DllImport("User32.Dll")]
            public static extern long SetCursorPos(int x, int y);

            [DllImport("User32.Dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
            //Mouse actions
            public const int MOUSEEVENTF_LEFTDOWN = 0x02;
            public const int MOUSEEVENTF_LEFTUP = 0x04;
            public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
            public const int MOUSEEVENTF_RIGHTUP = 0x10;

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }
        }

        /// <summary>
        /// Sets the label that tells the user how many mouse click events have happened.
        /// </summary>
        /// <param name="numClicks"></param>
        private void SetMouseClickLabel(int numClicks)
        {
            this.numMouseClicks.Text = $"{numClicks} Total Mouse Clicks";
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            run = false;
            mouseClicksThisRun = 0;
            SetMouseClickLabel(mouseClicksThisRun);
            EnableSettingFields();
        }

        #region Enable/Disable Setting Fields

        /// <summary>
        /// Allows the user to click the startButton, 
        /// maxClicksBetweenMovement text field, 
        /// minClicksBetweenMovement text field, 
        /// minWait text field,
        /// maxWait text field. 
        /// </summary>
        private void EnableSettingFields()
        {
            startButton.Enabled = true;
            maxClicksBetweenMovement.Enabled = true;
            minClicksBetweenMovement.Enabled = true;
            minWait.Enabled = true;
            maxWait.Enabled = true;
        }

        /// <summary>
        /// Disables the fields so user can't edit 
        /// maxClicksBetweenMovement text field, 
        /// minClicksBetweenMovement text field, 
        /// minWait text field,
        /// maxWait text field. 
        /// </summary>
        private void DisableSettingFields()
        {
            startButton.Enabled = false;
            maxClicksBetweenMovement.Enabled = false;
            minClicksBetweenMovement.Enabled = false;
            minWait.Enabled = false;
            maxWait.Enabled = false;
        }
        #endregion

        private void AutoClicker_Load(object sender, EventArgs e)
        {

        }
    }
}
