extends SceneTree

# 验证：已通关存档下点「继续游戏」会不会弹「游戏已通关」提示
func _initialize() -> void:
	root.get_node("GameState")
	var menu: Node = load("res://MainMenu.tscn").instantiate()
	root.add_child(menu)
	for i in range(8):
		await process_frame

	var btn: Button = menu.get_node("Buttons/ContinueButton")
	print("[Menu] 继续按钮可点=%s" % str(not btn.disabled))

	btn.emit_signal("pressed")
	await create_timer(0.4).timeout

	var dialogs := 0
	for c in menu.get_children():
		if c is ConfirmationDialog and c.visible:
			dialogs += 1
			print("[Menu] 弹窗标题=%s" % c.title)
			print("[Menu] 弹窗正文=%s" % c.dialog_text)
			print("[Menu] 确定按钮=%s  取消=%s" % [c.ok_button_text, c.cancel_button_text])
	print("[Menu] 可见确认框数量=%d  通关标记=%s" % [dialogs, str(root.get_node("GameState").Cleared)])

	var img := root.get_viewport().get_texture().get_image()
	if img != null: img.save_png("res://docs/screenshots/menu_cleared.png")
	quit()
