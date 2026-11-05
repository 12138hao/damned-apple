extends SceneTree

# 拍 jungle 里藤蔓所在的位置，确认它到底有没有画出来。
# 用法：改 TARGET 的 x 就能拍别处。

const TARGET := Vector2(480, 190)
const OUT := "res://docs/screenshots/vine_check.png"

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var cam := Camera2D.new()
	cam.zoom = Vector2(0.78, 0.78)
	cam.position_smoothing_enabled = false
	node.add_child(cam)
	cam.global_position = TARGET
	cam.make_current()

	for i in range(10):
		await process_frame

	var img := root.get_viewport().get_texture().get_image()
	if img == null:
		print("!! 拿不到视口"); quit(); return
	img.save_png(OUT)
	print("[Shot] 视口 ", img.get_size(), "  相机在 ", TARGET)

	for n in ["Vine1", "Vine2"]:
		var v: Node2D = node.get_node(n)
		print("%s  position=%s  Length=%s  TopPad=%s  屏幕y范围=[%.0f, %.0f]"
			% [n, v.global_position, v.get("Length"), v.get("TopPad"),
			   v.global_position.y + float(v.get("TopPad")),
			   v.global_position.y + float(v.get("Length"))])
	quit()
