use bevy::prelude::*;

/// A simple menu system plugin for Bevy that provides:
/// - Open/close via key (Esc by default)
/// - Optional pause while open (time scale = 0)
/// - Optional background dimmer with fade and close-on-click
/// - Named pages that can be shown/hidden by key
pub struct TPMenuSystemPlugin;

impl Plugin for TPMenuSystemPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<MenuConfig>()
            .init_resource::<MenuState>()
            .add_event::<PageShownEvent>()
            .add_systems(Startup, setup_menu_root)
            .add_systems(Update, (
                handle_toggle_input,
                handle_background_click,
                apply_menu_visibility,
                apply_page_visibility,
            ));
    }
}

#[derive(Resource, Reflect)]
#[reflect(Resource)]
pub struct MenuConfig {
    pub start_hidden: bool,
    pub pause_while_open: bool,
    pub close_on_background_click: bool,
    pub toggle_key: KeyCode,
    pub initial_page_key: Option<String>,
    pub set_first_focus_on_open: bool,
}

impl Default for MenuConfig {
    fn default() -> Self {
        Self {
            start_hidden: true,
            pause_while_open: true,
            close_on_background_click: false,
            toggle_key: KeyCode::Escape,
            initial_page_key: None,
            set_first_focus_on_open: true,
        }
    }
}

#[derive(Event, Debug, Clone)]
pub struct PageShownEvent(pub String);

#[derive(Resource, Default)]
pub struct MenuState {
    pub is_open: bool,
    pub current_page_key: Option<String>,
    pub menu_root: Option<Entity>,
    pub background_entity: Option<Entity>,
}

#[derive(Component, Clone)]
pub struct MenuPageKey(pub String);

#[derive(Component)]
pub struct MenuBackground;

#[derive(Component)]
pub struct MenuRoot;

fn setup_menu_root(mut commands: Commands, config: Res<MenuConfig>) {
    // Root node that holds the menu UI
    let root = commands
        .spawn((
            NodeBundle {
                style: Style {
                    width: Val::Percent(100.0),
                    height: Val::Percent(100.0),
                    position_type: PositionType::Absolute,
                    ..default()
                },
                background_color: Color::NONE.into(),
                visibility: if config.start_hidden {
                    Visibility::Hidden
                } else {
                    Visibility::Visible
                },
                ..default()
            },
            MenuRoot,
            Name::new("TPMenuSystem Root"),
        ))
        .id();

    // Optional background dimmer (starts transparent; user can attach interaction later)
    let background = commands
        .spawn((
            NodeBundle {
                style: Style {
                    width: Val::Percent(100.0),
                    height: Val::Percent(100.0),
                    ..default()
                },
                background_color: Color::rgba(0.0, 0.0, 0.0, 0.5).into(),
                z_index: ZIndex::Global(0),
                ..default()
            },
            Interaction::default(),
            MenuBackground,
            Name::new("Menu Background Dimmer"),
        ))
        .set_parent(root)
        .id();

    commands.insert_resource(MenuState {
        is_open: !config.start_hidden,
        current_page_key: None,
        menu_root: Some(root),
        background_entity: Some(background),
    });
}

pub fn register_page(commands: &mut Commands, parent: Entity, key: impl Into<String>, node: NodeBundle) -> Entity {
    let key_string = key.into();
    commands
        .spawn((node, MenuPageKey(key_string.clone()), Name::new(format!("Page: {}", key_string))))
        .set_parent(parent)
        .id()
}

fn handle_toggle_input(
    keyboard: Res<ButtonInput<KeyCode>>,
    config: Res<MenuConfig>,
    mut menu_state: ResMut<MenuState>,
    mut visibility_query: Query<&mut Visibility, With<MenuRoot>>,
) {
    if keyboard.just_pressed(config.toggle_key) {
        menu_state.is_open = !menu_state.is_open;
        if let Ok(mut vis) = visibility_query.get_single_mut() {
            *vis = if menu_state.is_open { Visibility::Visible } else { Visibility::Hidden };
        }
    }
}

fn handle_background_click(
    config: Res<MenuConfig>,
    mut menu_state: ResMut<MenuState>,
    mut interactions: Query<&Interaction, (Changed<Interaction>, With<MenuBackground>)>,
    mut visibility_query: Query<&mut Visibility, With<MenuRoot>>,
) {
    if !menu_state.is_open || !config.close_on_background_click {
        return;
    }
    for interaction in &mut interactions {
        if *interaction == Interaction::Pressed {
            menu_state.is_open = false;
            if let Ok(mut vis) = visibility_query.get_single_mut() {
                *vis = Visibility::Hidden;
            }
        }
    }
}

fn apply_menu_visibility(
    config: Res<MenuConfig>,
    menu_state: Res<MenuState>,
    mut time: ResMut<Time<Virtual>>, // Bevy 0.14 time types
    mut root_vis: Query<&mut Visibility, With<MenuRoot>>,
) {
    // Ensure visibility reflects state (in case changed elsewhere)
    if let Ok(mut vis) = root_vis.get_single_mut() {
        *vis = if menu_state.is_open { Visibility::Visible } else { Visibility::Hidden };
    }

    if config.pause_while_open {
        if menu_state.is_open {
            time.pause();
        } else {
            time.unpause();
        }
    }
}

fn apply_page_visibility(
    menu_state: Res<MenuState>,
    mut pages: Query<(&MenuPageKey, &mut Visibility), With<MenuPageKey>>,
) {
    // Show only the current page; hide others
    for (key, mut vis) in &mut pages {
        if let Some(current) = &menu_state.current_page_key {
            *vis = if &key.0 == current { Visibility::Visible } else { Visibility::Hidden };
        } else {
            *vis = Visibility::Hidden;
        }
    }
}

pub fn show_page(
    menu_state: &mut ResMut<MenuState>,
    writer: &mut EventWriter<PageShownEvent>,
    key: impl Into<String>,
) {
    let key_string = key.into();
    menu_state.current_page_key = Some(key_string.clone());
    writer.send(PageShownEvent(key_string));
}
