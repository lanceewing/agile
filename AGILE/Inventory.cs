using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AGI.Resource;

namespace AGILE
{
    /// <summary>
    /// The Inventory class handles the viewing of the player's inventory items.
    /// </summary>
    class Inventory
    {
        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// Holds the data and state for the user input, i.e. keyboard and mouse input.
        /// </summary>
        private UserInput userInput;

        /// <summary>
        /// Provides methods for drawing text on to the AGI screen.
        /// </summary>
        private TextGraphics textGraphics;

        /// <summary>
        /// The pixels array for the AGI screen, in which the text will be drawn.
        /// </summary>
        private int[] pixels;

        /// <summary>
        /// Constructor for Inventory.
        /// </summary>
        /// <param name="state">Holds all of the data and state for the Game currently running.</param>
        /// <param name="userInput">Holds the data and state for the user input, i.e. keyboard and mouse input.</param>
        /// <param name="textGraphics">Provides methods for drawing text on to the AGI screen.</param>
        /// <param name="pixels">The pixels array for the AGI screen, in which the text will be drawn.</param>
        public Inventory(GameState state, UserInput userInput, TextGraphics textGraphics, int[] pixels)
        {
            this.state = state;
            this.userInput = userInput;
            this.textGraphics = textGraphics;
            this.pixels = pixels;
        }

        /// <summary>
        /// Used during the drawing of the inventory screen to represent a single inventory
        /// item name displayed in a specified cell of the two column inventory table.
        /// </summary>
        class InvItem
        {
            public byte Num;
            public string Name;
            public int Row;
            public int Col;
        }

        /// <summary>
        /// Shows the inventory screen. Implements the AGI "status" command.
        /// </summary>
        public void ShowInventoryScreen()
        {
            List<InvItem> invItems = new List<InvItem>();
            byte selectedItemIndex = 0;
            int howMany = 0;
            int row = 2;

            // Switch to the text screen.
            textGraphics.TextScreen(15);

            // Construct the table of objects being carried, deciding where on
            // the screen they are to be printed as we go.
            for (byte i=0; i < state.Objects.Count; i++)
            {
                Object obj = state.Objects[i];
                if (obj.Room == Defines.CARRYING)
                {
                    InvItem invItem = new InvItem();
                    invItem.Num = i;
                    invItem.Name = obj.Name;
                    invItem.Row = row;

                    if ((howMany & 1) == 0)
                    {
                        invItem.Col = 1;
                    }
                    else
                    {
                        row++;
                        invItem.Col = 39 - invItem.Name.Length;
                    }

                    if (i == state.Vars[Defines.SELECTED_OBJ]) selectedItemIndex = (byte)invItems.Count;

                    invItems.Add(invItem);
                    howMany++;
                }
            }

            // If no objects in inventory, then say so.
            if (howMany == 0)
            {
                InvItem invItem = new InvItem();
                invItem.Num = 0;
                invItem.Name = "nothing";
                invItem.Row = row;
                invItem.Col = 16;
                invItems.Add(invItem);
            }

            // Display the inventory items.
            DrawInventoryItems(invItems, invItems[selectedItemIndex]);

            // If we are not allowing an item to be selected, we simply wait for a key press then return.
            if (!state.Flags[Defines.ENABLE_SELECT])
            {
                userInput.WaitForKey();
            }
            else
            {
                // Otherwise we handle movement between the items and selection of an item.
                while (true)
                {
                    int key = userInput.WaitForKey();
                    if (key == (int)Keys.Enter)
                    {
                        state.Vars[Defines.SELECTED_OBJ] = invItems[selectedItemIndex].Num;
                        break;
                    }
                    else if (key == (int)Keys.Escape)
                    {
                        state.Vars[Defines.SELECTED_OBJ] = 0xFF;
                        break;
                    }
                    else if ((key == (int)Keys.Up) || (key == (int)Keys.Down) || (key == (int)Keys.Right) || (key == (int)Keys.Left))
                    {
                        selectedItemIndex = MoveSelect(invItems, (Keys)key, selectedItemIndex);
                    }
                }
            }

            // Switch back to the graphics screen.
            textGraphics.GraphicsScreen();
        }

        /// <summary>
        /// Shows a special view of an object that has an attached description. Intended for use
        /// with the "look at object" scenario when the object looked at is an inventory item.
        /// </summary>
        /// <param name="viewNumber">The number of the view to show the special inventory object view of.</param>
        public void ShowInventoryObject(byte viewNumber)
        {
            // Set up the AnimatedObject that will be used to display this view.
            AnimatedObject aniObj = new AnimatedObject(state, -1);
            aniObj.SetView(viewNumber);
            aniObj.X = aniObj.PrevX = (short)((Defines.MAXX - aniObj.XSize) / 2);
            aniObj.Y = aniObj.PrevY = Defines.MAXY;
            aniObj.Priority = 15;
            aniObj.FixedPriority = true;
            aniObj.PreviousCel = aniObj.Cel;

            // Display the description in a window along with the item picture.
            textGraphics.WindowPrint(state.Views[viewNumber].Description, aniObj);

            // Restore the pixels that were behind the item's image.
            aniObj.RestoreBackPixels();
            aniObj.Show(pixels);
        }

        /// <summary>
        /// Draws the table of inventory items.
        /// </summary>
        /// <param name="invItems">The List of the items in the inventory table.</param>
        /// <param name="selectedItem">The currently selected item.</param>
        private void DrawInventoryItems(List<InvItem> invItems, InvItem selectedItem)
        {
            textGraphics.DrawString(this.pixels, "You are carrying:", 11 * 8, 0 * 8, 0, 15);

            foreach (InvItem invItem in invItems)
            {
                if ((invItem == selectedItem) && state.Flags[Defines.ENABLE_SELECT])
                {
                    textGraphics.DrawString(this.pixels, invItem.Name, invItem.Col * 8, invItem.Row * 8, 15, 0);
                }
                else
                {
                    textGraphics.DrawString(this.pixels, invItem.Name, invItem.Col * 8, invItem.Row * 8, 0, 15);
                }
            }

            if (state.Flags[Defines.ENABLE_SELECT])
            {
                textGraphics.DrawString(this.pixels, "Press ENTER to select, ESC to cancel", 2 * 8, 24 * 8, 0, 15);
            }
            else
            {
                textGraphics.DrawString(this.pixels, "Press a key to return to the game", 4 * 8, 24 * 8, 0, 15);
            }
        }

        /// <summary>
        /// Processes the direction key that has been pressed. If within the bounds of the
        /// inventory List, a new selected item index will be returned and a new inventory
        /// item highlighted on the screen.
        /// </summary>
        /// <param name="invItems"></param>
        /// <param name="dirKey"></param>
        /// <param name="oldSelectedItemIndex"></param>
        /// <returns>The index of the new selected inventory item.</returns>
        private byte MoveSelect(List<InvItem> invItems, Keys dirKey, byte oldSelectedItemIndex)
        {
            byte newSelectedItemIndex = oldSelectedItemIndex;

            switch (dirKey)
            {
                case Keys.Up:
                    newSelectedItemIndex -= 2;
                    break;
                case Keys.Right:
                    newSelectedItemIndex += 1;
                    break;
                case Keys.Down:
                    newSelectedItemIndex += 2;
                    break;
                case Keys.Left:
                    newSelectedItemIndex -= 1;
                    break;
            }

            if ((newSelectedItemIndex < 0) || (newSelectedItemIndex >= invItems.Count))
            {
                newSelectedItemIndex = oldSelectedItemIndex;
            }
            else
            {
                InvItem previousItem = invItems[oldSelectedItemIndex];
                InvItem newItem = invItems[newSelectedItemIndex];
                textGraphics.DrawString(this.pixels, previousItem.Name, previousItem.Col * 8, previousItem.Row * 8, 0, 15);
                textGraphics.DrawString(this.pixels, newItem.Name, newItem.Col * 8, newItem.Row * 8, 15, 0);
            }

            return newSelectedItemIndex;
        }
    }
}
