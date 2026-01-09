# tp_menu_system_rust

Bevy plugin that provides a simple menu system with multiple pages, optional dimmer background with fade, close-on-click, open/close via Esc, and pause while open.

## Features
- Open/close menu (Esc)
- Pause `Time` while open (optional)
- Background dimmer with fade (optional)
- Close on dimmer click (optional)
- Page switching by string key (events: `PageShownEvent`)

## Usage
Add to Cargo.toml:
```toml
[dependencies]
tp_menu_system_rust = { path = "../tp_menu_system_rust" }
```

Add the plugin and configure:
```rust
use bevy::prelude::*;
use tp_menu_system_rust::*;

fn main() {
    App::new()
        .add_plugins((DefaultPlugins, TPMenuSystemPlugin))
        .insert_resource(MenuConfig {
            start_hidden: false,
            pause_while_open: true,
            close_on_background_click: true,
            toggle_key: KeyCode::Escape,
            initial_page_key: Some("Main".to_string()),
            set_first_focus_on_open: false,
        })
        .run();
}
```

Create pages under the `MenuRoot`:
```rust
fn setup_pages(mut commands: Commands, menu_root: Query<Entity, With<MenuRoot>>) {
    let root = menu_root.single();
    let _main = register_page(&mut commands, root, "Main", NodeBundle::default());
    let _settings = register_page(&mut commands, root, "Settings", NodeBundle::default());
}
```

Switch pages at runtime:
```rust
fn input_system(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut menu_state: ResMut<MenuState>,
    mut writer: EventWriter<PageShownEvent>,
) {
    if keyboard.just_pressed(KeyCode::Digit1) { show_page(&mut menu_state, &mut writer, "Main"); }
    if keyboard.just_pressed(KeyCode::Digit2) { show_page(&mut menu_state, &mut writer, "Settings"); }
}
```

## Demo
Run the included example:
```bash
cargo run --example demo
```
