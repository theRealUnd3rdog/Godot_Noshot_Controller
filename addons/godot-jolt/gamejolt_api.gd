@tool extends EditorPlugin

func _enter_tree() -> void: # When this plugin node enters the tree
	add_custom_type("GameJoltAPI", "HTTPRequest", preload("main.gd"), preload("gj_icon.png"))

func _exit_tree() -> void: # When this plugin node exits the tree
	remove_custom_type("GameJoltAPI")
