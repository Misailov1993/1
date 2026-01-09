use bevy::prelude::*;
use tp_menu_system_rust::*;

fn main() {
    App::new()
        .add_plugins((
            DefaultPlugins.set(WindowPlugin {
                primary_window: Some(Window {
                    title: "TP Menu System Demo".into(),
                    resolution: (1280., 720.).into(),
                    ..default()
                }),
                ..default()
            }),
            TPMenuSystemPlugin,
        ))
        .insert_resource(MenuConfig {
            start_hidden: false,
            pause_while_open: true,
            close_on_background_click: true,
            toggle_key: KeyCode::Escape,
            initial_page_key: Some("Main".to_string()),
            set_first_focus_on_open: false,
        })
        .add_systems(Startup, (setup_camera, setup_demo_pages))
        .add_systems(Update, demo_input_system)
        .run();
}

fn setup_camera(mut commands: Commands) {
    commands.spawn(Camera2dBundle::default());
}

fn setup_demo_pages(
    mut commands: Commands,
    menu_root_query: Query<Entity, With<MenuRoot>>,
    asset_server: Res<AssetServer>,
) {
    let Ok(root) = menu_root_query.get_single() else { return; };

    // Page: Main
    let main_page = register_page(
        &mut commands,
        root,
        "Main",
        NodeBundle {
            style: Style {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                justify_content: JustifyContent::Center,
                align_items: AlignItems::Center,
                ..default()
            },
            background_color: Color::rgba(0.05, 0.05, 0.08, 0.9).into(),
            ..default()
        },
    );

    // Add a text label to main page
    commands.entity(main_page).with_children(|p| {
        p.spawn(TextBundle::from_section(
            "Main Page (press 1/2 to switch, Esc to toggle)",
            TextStyle {
                font: asset_server.load("fonts/FiraSans-Bold.ttf"),
                font_size: 28.0,
                color: Color::WHITE,
            },
        ));
    });

    // Page: Settings
    let settings_page = register_page(
        &mut commands,
        root,
        "Settings",
        NodeBundle {
            style: Style {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                justify_content: JustifyContent::Center,
                align_items: AlignItems::Center,
                ..default()
            },
            background_color: Color::rgba(0.08, 0.05, 0.05, 0.9).into(),
            ..default()
        },
    );

    commands.entity(settings_page).with_children(|p| {
        p.spawn(TextBundle::from_section(
            "Settings Page (press 1/2 to switch, Esc to toggle)",
            TextStyle {
                font: asset_server.load("fonts/FiraSans-Bold.ttf"),
                font_size: 28.0,
                color: Color::WHITE,
            },
        ));
    });
}

fn demo_input_system(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut menu_state: ResMut<MenuState>,
    mut page_shown_writer: EventWriter<PageShownEvent>,
) {
    if keyboard.just_pressed(KeyCode::Digit1) {
        show_page(&mut menu_state, &mut page_shown_writer, "Main");
    }
    if keyboard.just_pressed(KeyCode::Digit2) {
        show_page(&mut menu_state, &mut page_shown_writer, "Settings");
    }
}
