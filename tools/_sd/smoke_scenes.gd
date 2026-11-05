extends SceneTree
func _initialize() -> void:
	var scenes := ["firstry","jungle","jungleinside","house_inside","rockhouseinside","underground","bossarena","MainMenu","OldApple","PirateApple","UndeadPirateApple","Vine","RevivePoint","SkeletonHand","VehiclePickup"]
	var bad := 0
	for s in scenes:
		var p := "res://%s.tscn" % s
		if not ResourceLoader.exists(p):
			print("  -  不存在 ", p); continue
		var ps = load(p)
		if ps == null:
			print("  !! 加载失败 ", p); bad += 1
		else:
			var n: Node = ps.instantiate()
			if n == null:
				print("  !! 实例化失败 ", p); bad += 1
			else:
				n.free()
				print("  ok ", p)
	print("")
	print("失败 %d 个" % bad)
	quit()
