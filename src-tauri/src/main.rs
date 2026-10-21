// Release derlemesinde ek konsol penceresi açılmasın.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    clipboard_manager_lib::run()
}
