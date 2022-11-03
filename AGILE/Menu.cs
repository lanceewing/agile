using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AGILE.TextGraphics;

namespace AGILE
{
    /// <summary>
    /// The Menu class is responsible for processing both the AGI commands that define the
    /// menus and their items and also for rendering the menu system when it is activated and
    /// processing the navigation and selection events while it is open.
    /// </summary>
    class Menu
    {
        // Various constants for calculating menu window dimensions and position.
        private const int CHARWIDTH = 4;
        private const int CHARHEIGHT = 8;
        private const int VMARGIN = 8;
        private const int HMARGIN = CHARWIDTH;

        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// The pixels array for the AGI screen, in which the text will be drawn.
        /// </summary>
        private int[] pixels;

        /// <summary>
        /// Holds the data and state for the user input, i.e. keyboard and mouse input.
        /// </summary>
        private UserInput userInput;

        /// <summary>
        /// Provides methods for drawing text on to the AGI screen.
        /// </summary>
        private TextGraphics textGraphics;

        /// <summary>
        /// The List of the top level menu headers currently defined in the menu system.
        /// </summary>
        private List<MenuHeader> headers;

        /// <summary>
        /// The currently highlighted item in the currently open menu header.
        /// </summary>
        private MenuItem currentItem;

        /// <summary>
        /// The currently open menu header, i.e. the open whose items are currently being displayed.
        /// </summary>
        private MenuHeader currentHeader;

        private int menuCol;
        private int itemRow;
        private int itemCol;

        /// <summary>
        /// If set to true then this prevents further menu definition commands from being processed.
        /// </summary>
        private bool menuSubmitted;

        class MenuHeader
        {
            public MenuItem Title;
            public List<MenuItem> Items;
            public MenuItem CurrentItem;
            public int Height;
        }

        class MenuItem
        {
            public string Name;
            public int Row;
            public int Col;
            public bool Enabled;
            public int Controller;
        }

        /// <summary>
        /// Constructor for Menu.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="textGraphics"></param>
        /// <param name="pixels"></param>
        /// <param name="userInput"></param>
        public Menu(GameState state, TextGraphics textGraphics, int[] pixels, UserInput userInput)
        {
            this.state = state;
            this.textGraphics = textGraphics;
            this.headers = new List<MenuHeader>();
            this.pixels = pixels;
            this.userInput = userInput;
        }

        /// <summary>
        /// Creates a new menu with the given name.
        /// </summary>
        /// <param name="menuName">The name of the new menu.</param>
        public void SetMenu(string menuName)
        {
            // We can't accept any more menu definitions if submit.menu has already been executed.
            if (menuSubmitted) return;

            if (currentHeader == null)
            {
                // The first menu header starts at column 1.
                menuCol = 1;
            }
            else if (currentHeader.Items.Count == 0)
            {
                // If the last header didn't have any items, then disable it.
                currentHeader.Title.Enabled = false;
            }

            // Create a new MenuHeader.
            MenuHeader header = new AGILE.Menu.MenuHeader();

            // Set the position of this menu name in the menu strip (leave two  
            // chars between menu titles).
            header.Title = new MenuItem();
            header.Title.Row = 0;
            header.Title.Name = menuName;
            header.Title.Col = menuCol;
            header.Title.Enabled = true;
            header.Items = new List<MenuItem>();
            header.Height = 0;

            this.currentHeader = header;
            this.headers.Add(header);

            // Adjust the menu column for the next header.
            menuCol += menuName.Length + 1;

            // Initialize stuff for the menu items to follow.
            currentItem = null;
            itemRow = 1;
        }

        /// <summary>
        /// Creates a new menu item in the current menu, of the given name and mapped
        /// to the given controller number.
        /// </summary>
        /// <param name="itemName">The name of the new menu item.</param>
        /// <param name="controller">The number of the controller to map this menu item to.</param>
        public void SetMenuItem(string itemName, int controller)
        {
            // We can't accept any more menu definitions if submit.menu has already been executed.
            if (menuSubmitted) return;

            // Create and define the new menu item and its position.
            MenuItem menuItem = new MenuItem();
            menuItem.Name = itemName;
            menuItem.Controller = controller;
            if (itemRow == 1)
            {
                if (currentHeader.Title.Col + itemName.Length < 39)
                {
                    itemCol = currentHeader.Title.Col;
                }
                else
                {
                    itemCol = 39 - itemName.Length;
                }
            }
            menuItem.Row = ++itemRow;
            menuItem.Col = itemCol;
            menuItem.Enabled = true;

            // Add the menu item to the current header's item list.
            currentItem = menuItem;
            currentHeader.Items.Add(menuItem);
            currentHeader.Height++;
            if (currentHeader.CurrentItem == null)
            {
                currentHeader.CurrentItem = menuItem;
            }
        }

        /// <summary>
        /// Signals to the menu system that the menu has now been fully defined. No further SetMenu
        /// or SetMenuItem calls will be processed. The current header and item is reset back to the
        /// first item in the first menu, ready for usage when the menu is activated.
        /// </summary>
        public void SubmitMenu()
        {
            // If the last menu didn't have any items, disable it.
            if (currentHeader.Items.Count == 0)
            {
                currentHeader.Title.Enabled = false;
            }

            // Make the first menu the current one.
            currentHeader = (headers.Count > 0? headers[0] : null);
            currentItem = ((currentHeader != null) && (currentHeader.Items.Count > 0) ? currentHeader.Items[0] : null);

            // Remember that the submit has happened. We can't process menu definitions after submit.menu
            menuSubmitted = true;
        }

        /// <summary>
        /// Enables all MenuItems that map to the given controller number.
        /// </summary>
        /// <param name="controller">The controller whose menu items should be enabled.</param>
        public void EnableItem(int controller)
        {
            foreach  (MenuHeader header in headers)
            {
                foreach (MenuItem item in header.Items)
                {
                    if (item.Controller == controller)
                    {
                        item.Enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Enables all MenuItems.
        /// </summary>
        public void EnableAllMenus()
        {
            foreach (MenuHeader header in headers)
            {
                foreach (MenuItem item in header.Items)
                {
                    item.Enabled = true;
                }
            }
        }

        /// <summary>
        /// Disables all MenuItems that map to the given controller number.
        /// </summary>
        /// <param name="controller">The controller whose menu items should be disabled.</param>
        public void DisableItem(int controller)
        {
            foreach (MenuHeader header in headers)
            {
                foreach (MenuItem item in header.Items)
                {
                    if (item.Controller == controller)
                    {
                        item.Enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Opens the menu system and processes all the navigation events until an item is either
        /// selected or the ESC key is pressed.
        /// </summary>
        public void MenuInput()
        {
            // Not sure why there is an ENABLE_MENU flag and the allow.menu command, but there is.
            if (state.Flags[Defines.ENABLE_MENU] && state.MenuEnabled)
            {
                // Clear the menu bar to white.
                textGraphics.ClearLines(0, 0, 15);

                // Draw each of the header titles in deselected mode.
                foreach (MenuHeader header in headers) Deselect(header.Title);

                // Starts by showing the currently selected menu header and item.
                ShowMenu(currentHeader);

                // Now we process all navigation keys until we the user either makes a selection
                // or exits the menu system.
                while (true)
                {
                    int index;

                    switch (userInput.WaitForKey())
                    {
                        case (int)Keys.Enter:             // Select the currently highlighted menu item.
                            if (!currentItem.Enabled) continue;
                            state.Controllers[currentItem.Controller] = true;
                            PutAwayMenu(currentHeader, currentItem);
                            RestoreMenuLine();
                            state.MenuOpen = false;
                            return;

                        case (int)Keys.Escape:            // Exit the menu system without a selection.
                            PutAwayMenu(currentHeader, currentItem);
                            RestoreMenuLine();
                            state.MenuOpen = false;
                            return;

                        case (int)Keys.Up:                // Moving up within current menu.
                            Deselect(currentItem);
                            index = (currentHeader.Items.IndexOf(currentItem) + currentHeader.Items.Count - 1) % currentHeader.Items.Count;
                            currentItem = currentHeader.Items[index];
                            Select(currentItem);
                            break;

                        case (int)Keys.PageUp:             // Move to top item of current menu.
                            Deselect(currentItem);
                            currentItem = currentHeader.Items.First();
                            Select(currentItem);
                            break;

                        case (int)Keys.Right:              // Move to the menu on the right of the current menu..
                            PutAwayMenu(currentHeader, currentItem);
                            index = headers.IndexOf(currentHeader);
                            do { currentHeader = headers[(index = ((index + 1) % headers.Count))]; }
                            while (!currentHeader.Title.Enabled);
                            currentItem = currentHeader.CurrentItem;
                            ShowMenu(currentHeader);
                            break;

                        case (int)Keys.PageDown:           // Move to bottom item of current menu.
                            Deselect(currentItem);
                            currentItem = currentHeader.Items.Last();
                            Select(currentItem);
                            break;

                        case (int)Keys.Down:               // Move down within current menu.
                            Deselect(currentItem);
                            index = (currentHeader.Items.IndexOf(currentItem) + 1) % currentHeader.Items.Count;
                            currentItem = currentHeader.Items[index];
                            Select(currentItem);
                            break;

                        case (int)Keys.End:                // Move to the rightmost menu.
                            PutAwayMenu(currentHeader, currentItem);
                            currentHeader = headers.Last();
                            currentItem = currentHeader.CurrentItem;
                            ShowMenu(currentHeader);
                            break;

                        case (int)Keys.Left:               // Move left within current menu.
                            PutAwayMenu(currentHeader, currentItem);
                            index = headers.IndexOf(currentHeader);
                            do { currentHeader = headers[(index = ((index + headers.Count - 1) % headers.Count))]; }
                            while (!currentHeader.Title.Enabled);
                            currentItem = currentHeader.CurrentItem;
                            ShowMenu(currentHeader);
                            break;

                        case (int)Keys.Home:               // Move to the leftmost menu.
                            PutAwayMenu(currentHeader, currentItem);
                            currentHeader = headers.First();
                            currentItem = currentHeader.CurrentItem;
                            ShowMenu(currentHeader);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Restores the state of what the menu line would have looked like prior to the menu being activated.
        /// </summary>
        private void RestoreMenuLine()
        {
            if (state.ShowStatusLine)
            {
                textGraphics.UpdateStatusLine();
            }
            else
            {
                textGraphics.ClearLines(0, 0, 0);
            }
        }

        /// <summary>
        /// Shows the menu items for the given MenuHeader.
        /// </summary>
        /// <param name="header">The MenuHeader to show the menu items of.</param>
        private void ShowMenu(MenuHeader header)
        {
            // Interestingly, it would seem that the width is always calculated using the first item. The
            // original AGI games tended to make the item names a consistent length within each menu.
            MenuItem firstItem = (header.Items.Count > 0 ? header.Items[0] : null);
            int height = header.Height;
            int width = (firstItem != null ? firstItem.Name.Length : header.Title.Name.Length);
            int column = (firstItem != null ? firstItem.Col : header.Title.Col);

            // Compute window size and position and put them into the appropriate bytes of the words.
            int menuDim = ((height * CHARHEIGHT + 2 * VMARGIN) << 8) | (width * CHARWIDTH + 2 * HMARGIN);
            int menuPos = (((column - 1) * CHARWIDTH) << 8) | ((height + 1) * CHARHEIGHT + VMARGIN - 1);

            // Show the menu title as being selected.
            Select(header.Title);

            // Open a window for this menu using the calculated position and dimensions.
            textGraphics.OpenWindow(new TextWindow(menuPos, menuDim, 15, 0));

            // Render each of the items in this menu.
            foreach (MenuItem item in header.Items)
            {
                if (item == header.CurrentItem)
                {
                    Select(item);
                }
                else
                {
                    Deselect(item);
                }
            }
        }

        /// <summary>
        /// Puts away the menu so that it is no longer displayed, but remembers what item
        /// in the list was selected at the time it was put away.
        /// </summary>
        /// <param name="header">The MenuHeader representing the menu to put away.</param>
        /// <param name="item">The MenuItem that was currently selected in the menu when it was put away.</param>
        private void PutAwayMenu(MenuHeader header, MenuItem item)
        {
            header.CurrentItem = item;
            Deselect(header.Title);
            textGraphics.CloseWindow();
        }

        /// <summary>
        /// Renders the given MenuItem in a selected state.
        /// </summary>
        /// <param name="item">The MenuItem to render in the selected state.</param>
        private void Select(MenuItem item)
        {
            textGraphics.DrawString(pixels, item.Name, item.Col * 8, item.Row * 8, 15, 0, !item.Enabled);
        }

        /// <summary>
        /// Renders the given MenuItem in a deselected state.
        /// </summary>
        /// <param name="item">The MenuItem to render in the deselected state.</param>
        private void Deselect(MenuItem item)
        {
            textGraphics.DrawString(pixels, item.Name, item.Col * 8, item.Row * 8, 0, 15, !item.Enabled);
        }
    }
}
