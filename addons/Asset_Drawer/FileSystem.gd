@tool
extends EditorPlugin

# Padding from the bottom when popped out
var padding: int = 20

# The file system
var FileDock: Object

# Toggle for when the file system is moved to bottom
var filesBottom: bool = false

var newSize: Vector2
var initialLoad: bool = false

var AssetDrawerShortcut: InputEventKey = InputEventKey.new()
var showing: bool = false

func _enter_tree() -> void:
	# Add tool button to move shelf to editor bottom
	add_tool_menu_item("Files to Bottom", Callable(self, "FilesToBottom"))
	
	# Get our file system
	FileDock = self.get_editor_interface().get_file_system_dock()
	await get_tree().create_timer(0.1).timeout
	FilesToBottom()

#region show hide filesystem
func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		if (Input.is_key_pressed(KEY_SPACE) &&
		Input.is_key_pressed(KEY_CTRL)):
			if filesBottom == true:
				match showing:
					false:
						make_bottom_panel_item_visible(FileDock)
						showing = true
					true:
						print("hide")
						hide_bottom_panel()
						showing = false
#endregion

func _exit_tree() -> void:
	remove_tool_menu_item("Files to Bottom")
	FilesToBottom()


func _process(delta: float) -> void:
	
	newSize = FileDock.get_window().size
	
	# Keeps the file system from being unusable in size
	if FileDock.get_window().name == "root" && filesBottom == false:
		FileDock.get_child(3).get_child(0).size.y = newSize.y - padding
		FileDock.get_child(3).get_child(1).size.y = newSize.y - padding
		return
		
	# Adjust the size of the file system based on how far up
	# the drawer has been pulled
	if FileDock.get_window().name == "root" && filesBottom == true:
		newSize = FileDock.get_parent().size
		FileDock.get_child(3).get_child(0).size.y = newSize.y - 60
		FileDock.get_child(3).get_child(1).size.y = newSize.y - 60
		return
	
	# Keeps our systems sized when popped out
	if (FileDock.get_window().name != "root" && filesBottom == false):
		FileDock.get_window().min_size.y = 50
		FileDock.get_child(3).get_child(0).size.y = newSize.y - padding
		FileDock.get_child(3).get_child(1).size.y = newSize.y - padding
		
		# Centers window on first pop
		if initialLoad == false:
			initialLoad = true
			var screenSize: Vector2 = DisplayServer.screen_get_size()
			FileDock.get_window().position = screenSize/2
			
		return

# Moves the files between the bottom panel and the original dock
func FilesToBottom() -> void:
	if filesBottom == true:
		remove_control_from_bottom_panel(FileDock)
		add_control_to_dock(EditorPlugin.DOCK_SLOT_LEFT_BR, FileDock)
		filesBottom = false
		return

	FileDock = self.get_editor_interface().get_file_system_dock()
	remove_control_from_docks(FileDock)
	add_control_to_bottom_panel(FileDock, "File System")
	filesBottom = true

