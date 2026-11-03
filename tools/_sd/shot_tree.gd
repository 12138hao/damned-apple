extends SceneTree

var frame := 0
var menu: Node

func _initialize():
	menu = load("res://MainMenu.tscn").instantiate()
	root.add_child(menu)
	current_scene = menu

func _process(delta):
	frame += 1
	if frame == 90:
		RenderingServer.force_draw()
		root.get_texture().get_image().save_png("res://tools/_sd/menu_tree.png")
		print("[Shot] 完成")
		quit()
	return false
