extends SceneTree

# 分层诊断：分别拍「全开 / 只留藤 / 只留背景」
func _initialize() -> void:
	var node: Node = load("res://eden.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	for i in range(90):
		await process_frame

	var vp := root.get_viewport()
	vp.get_texture().get_image().save_png("res://docs/screenshots/_diag_full.png")

	# 只留藤：关掉程序化背景（含草地/植被/云）
	var backdrop := node.get_node_or_null("EdenBackdrop")
	if backdrop:
		backdrop.visible = false
	for i in range(5):
		await process_frame
	vp.get_texture().get_image().save_png("res://docs/screenshots/_diag_vineonly.png")

	# 只留背景：藤藏起来
	if backdrop:
		backdrop.visible = true
	for name in ["VineRun", "VineBend"]:
		var n := node.get_node_or_null(name)
		if n:
			n.visible = false
	for i in range(5):
		await process_frame
	vp.get_texture().get_image().save_png("res://docs/screenshots/_diag_bgonly.png")

	print("[Diag] 三张已出")
	quit()
