extends SceneTree

# 音频探针：进 jungle，把玩家丢进泥浆、引苹果怪开打，
# 逐帧盯着 AudioManager 里那 8 个播放器，谁开始播就打印它的资源路径。
#
# 目的：确认「除了 box 本体的声音，jungle 里一声都不响」。

var _heard := {}

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	var enemy: CharacterBody2D = node.get_node_or_null("undead_apple1")
	if box == null or enemy == null:
		print("!! 找不到节点"); quit(); return

	var am := root.get_node_or_null("AudioManager")
	if am == null:
		print("!! 没有 AudioManager autoload"); quit(); return

	# 站到苹果怪左边的泥浆里（泥浆 x 范围大约 1227~2811）
	box.global_position = enemy.global_position + Vector2(-220, -30)
	enemy.set("RandomPatrol", false)
	enemy.set("Speed", 0.0)

	var players := []
	for c in am.get_children():
		if c is AudioStreamPlayer:
			players.append(c)
	print("播放器数量 = ", players.size())

	var last := {}
	for i in range(players.size()):
		last[i] = false

	for f in range(720):
		await physics_frame

		# 让玩家在泥里左右走，触发泥浆脚步声
		var vx := 60.0 if (f / 60) % 2 == 0 else -60.0
		box.velocity.x = vx

		for i in range(players.size()):
			var p: AudioStreamPlayer = players[i]
			if p.playing and not last[i]:
				var path := "?"
				if p.stream != null: path = str(p.stream.resource_path)
				if not _heard.has(path):
					_heard[path] = 0
				_heard[path] += 1
				print("  f%-4d 播放 %s" % [f, path])
			last[i] = p.playing

	print("--- 这 12 秒里听到过的音频 ---")
	if _heard.is_empty():
		print("  （一声都没有）")
	for k in _heard:
		print("  ", k, "  ×", _heard[k])
	quit()
