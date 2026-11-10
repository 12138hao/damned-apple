extends SceneTree

# 确认退出结局后进下一关会把 BGM 换成新场景的曲子
func _initialize() -> void:
	var am: Node = root.get_node_or_null("AudioManager")
	if am == null:
		quit()
		return
	am.PlayBgm("ending", 0.1)
	await create_timer(0.3).timeout
	print("[Bgm] 主菜单 =%s" % str(am.CurrentBgm))
	am.PlayBgmForScene("res://firstry.tscn")
	await create_timer(0.3).timeout
	print("[Bgm] 进第一关=%s" % str(am.CurrentBgm))
	am.PlayBgmForScene("res://eden.tscn")
	await create_timer(0.3).timeout
	print("[Bgm] 进伊甸园=%s" % str(am.CurrentBgm))
	quit()
