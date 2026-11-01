# Tài Liệu Thiết Kế Game - Color Bus Sort Puzzle

Phiên bản: 1.0  
Ngày: 28/05/2026  
Game tham chiếu: Color Bus Trip, Google Play package `com.Tinkerbox.ColorTrip`  
Mục tiêu tài liệu: phân tích game tham chiếu và tạo một bản GDD để phát triển một game puzzle xe bus màu sắc có gameplay tương tự, nhưng không sao chép tên, logo, art, level, UI, screenshot, store asset hay nội dung độc quyền của game gốc.

## 1. Tóm Tắt Game Tham Chiếu

Thông tin công khai trên Google Play được kiểm tra ngày 28/05/2026:

- Tên game: Color Bus Trip
- Nhà phát hành hiển thị: VOODOO
- Thể loại: Puzzle
- Mô tả gameplay chính: chạm vào xe bus, ghép màu hành khách, hoàn thành việc đón/trả khách.
- Cơ chế công khai: tap xe bus để đưa xe vào trạm trống gần nhất; khách sai màu sẽ xuống xe; khách đúng màu sẽ lên xe; xe đầy đúng màu thì rời khỏi khu vực chơi.
- Rating hiển thị: khoảng 4.6 sao
- Lượt tải hiển thị: 1M+
- Cập nhật gần nhất: 19/05/2026
- Monetization hiển thị: in-app purchases, có luồng cài đặt miễn phí.
- Content rating: Everyone
- Nguồn: https://play.google.com/store/apps/details?id=com.Tinkerbox.ColorTrip

Kết luận thiết kế: đây là game hyper-casual / hybrid-casual puzzle. Điểm mạnh nằm ở input cực đơn giản, hình ảnh dễ đọc, level ngắn, cảm giác sắp xếp màu thỏa mãn, dễ chơi lại và có thể kiếm tiền bằng ads, IAP, booster.

## 2. Hướng Sản Phẩm Và Lưu Ý Pháp Lý

Không nên làm bản copy 1:1. Nên làm một game mới có sức hút tương tự:

- Giữ lại: sorting theo màu, xe bus, một chạm để ra quyết định, level ngắn, gameplay dễ hiểu.
- Thay đổi: tên game, hình dáng xe, hình dáng hành khách, cách bố trí trạm, background, UI, layout level, progression map, store copy, icon, âm thanh, timing animation, tên booster và cách đóng gói monetization.
- Tên tạm đề xuất: Transit Tint, Hue Shuttle, Color Terminal, Bus Palette Jam.
- Hướng nhận diện đề xuất: trạm trung chuyển nhỏ dạng toy-like diorama, có biển báo riêng, vạch đường riêng, chủ đề thành phố riêng.

## 3. Fantasy Chính Của Game

Người chơi đóng vai người điều phối xe bus. Nhiệm vụ là giải các bài toán giao thông nhỏ bằng cách đưa xe bus màu sắc vào trạm, cho khách sai màu xuống, đón khách đúng màu và đưa xe rời khỏi bàn khi đã đầy ghế.

Cảm giác cần tạo cho người chơi:

- "Tôi hiểu cách chơi chỉ sau 2 giây."
- "Nước này thật đã."
- "Nếu tính trước một bước, tôi có thể giải được."
- "Chơi thêm một level nữa."

## 4. Đối Tượng Người Chơi

- Chính: người chơi casual puzzle trên mobile, từ 8 tuổi trở lên.
- Session: 30 giây đến 3 phút mỗi level.
- Kỹ năng: nhìn màu, sắp xếp hàng đợi, lập kế hoạch nhẹ.
- Thiết bị: ưu tiên Android tầm trung/thấp, mục tiêu 60 FPS, màn hình dọc.

## 5. Core Loop

1. Người chơi nhìn thấy nhiều xe bus, hành khách và các trạm trống/đã có xe.
2. Người chơi tap vào một xe bus có thể di chuyển.
3. Xe bus chạy vào trạm trống hợp lệ gần nhất.
4. Xe cho khách sai màu xuống trước.
5. Khách đúng màu lên xe cho đến khi hết chỗ hoặc hết khách phù hợp.
6. Xe đầy khách đúng màu thì chạy ra khỏi bàn.
7. Người chơi lặp lại đến khi hoàn thành mục tiêu.
8. Win screen hiện thưởng coin/star/progression và cho sang level tiếp theo nhanh.

Thất bại hoặc soft-fail:

- Hard fail: không còn xe nào đi được và tất cả trạm đều bị khóa.
- Hướng casual nên dùng: game tạm dừng khi bị deadlock và đề xuất Undo, Shuffle, Extra Station hoặc xem quảng cáo để tiếp tục.

## 6. Luật Chơi Chính

Thành phần:

- Bus: có `colorId`, `capacity`, danh sách khách hiện tại, trạng thái di chuyển và vị trí lane.
- Passenger: có `colorId`, vị trí và trạng thái: waiting, boarding, seated, exiting, removed.
- Station/Bay: điểm dừng tạm thời, mỗi trạm chỉ chứa một xe.
- Exit Gate: đường ra của xe đã hoàn thành.
- Queue/Lane: hệ thống hàng đợi quyết định xe nào có thể được tap hoặc bị chặn.

Luật:

- Xe được tap phải ở trạng thái selectable, không bị chặn, không đang di chuyển và có trạm hợp lệ.
- Xe đi đến trạm trống gần nhất theo khoảng cách đường đi, không chỉ theo khoảng cách trên màn hình.
- Khách sai màu trên xe xuống trước.
- Khách đúng màu đang chờ sẽ lên xe nếu có thể tiếp cận và xe còn ghế.
- Xe rời bàn ngay khi tất cả ghế được lấp đầy bằng khách đúng màu.
- Level hoàn thành khi tất cả khách mục tiêu được chở đi hoặc tất cả xe đã được clear.

Sức chứa đề xuất:

- Early game: 3-4 ghế.
- Mid game: 4-6 ghế.
- Late game: 6-8 ghế kèm cơ chế đặc biệt.

## 7. Điều Khiển

- Tap xe bus: chọn và điều xe.
- Tap nút booster: kích hoạt hành động hỗ trợ.
- Không nên có camera drag trong MVP; bàn chơi nên hiện trọn vẹn trên màn hình.
- Không cần joystick, không cần swipe.

Feedback input:

- Xe hợp lệ: bounce/highlight nhẹ khi tap.
- Xe không hợp lệ: rung đỏ, icon bị chặn, haptic ngắn.
- Đường đi của xe: lane glow hoặc mũi tên ngắn.
- Hoàn thành xe: còi xe nhỏ, passenger cheer, coin burst.

## 8. Hệ Thống Thiết Kế Level

Biến số độ khó:

- Số lượng màu.
- Số lượng xe bus.
- Sức chứa xe bus.
- Số slot trạm.
- Số khách sai màu có sẵn trong xe.
- Thứ tự hàng đợi hành khách.
- Lane bị chặn hoặc lane một chiều.
- Xe ẩn, chỉ hiện sau khi clear xe trước.
- Xe đặc biệt có kích thước lớn.

Pacing level:

- Level 1-5: dạy tap xe, ghép màu, xe đầy thì rời bàn.
- Level 6-15: thêm khách sai màu và trạm giới hạn.
- Level 16-30: thêm planning nhiều màu và phụ thuộc thứ tự xe.
- Level 31-60: thêm blocker, xe lớn, booster Extra Station.
- Level 61+: xoay vòng mechanic trong event, daily challenge và hard level.

Nguyên tắc vàng: tất cả puzzle phải có thể giải mà không cần booster. Booster dùng để sửa sai hoặc giảm friction, không được bắt buộc.

## 9. Mẫu Level Tham Khảo

Tutorial Level 1:

- Màu: đỏ, xanh dương
- Xe: xe đỏ 3 ghế, xe xanh dương 3 ghế
- Trạm: 1
- Khách: 3 đỏ, 3 xanh dương
- Mục tiêu: dạy việc xe đầy đúng màu sẽ rời bàn.

Planning Level 8:

- Màu: đỏ, xanh dương, vàng
- Xe: xe đỏ có sẵn 1 khách xanh; xe xanh có sẵn 1 khách đỏ.
- Trạm: 1
- Mục tiêu: dạy việc cho khách sai màu xuống tạo cơ hội cho nước sau.

Deadlock Training Level 14:

- Màu: 3
- Trạm: 2
- Một xe chặn đường xe khác.
- Mục tiêu: dạy thứ tự điều xe.

Hard Level 35:

- Màu: 4
- Xe: 7
- Trạm: 2
- Queue ẩn: 2 xe mới xuất hiện sau khi xe trước rời bàn.
- Mục tiêu: tạo chuỗi giải nhiều bước có cảm giác thỏa mãn.

## 10. Booster

Undo:

- Hoàn tác lần điều xe gần nhất.
- Tutorial cho miễn phí 3 lần; về sau dùng coin, ad hoặc IAP.

Extra Station:

- Thêm một trạm tạm thời trong level hiện tại.
- Rất hợp với rewarded ad.

Swap Passengers:

- Đổi vị trí 2 hành khách trong queue hoặc trong xe.
- Cần dùng cẩn thận vì có thể làm puzzle khó đọc.

Paint Ticket:

- Đổi màu một hành khách thành màu của xe được chọn.
- Booster mạnh, chỉ nên mở ở late-game.

Hint:

- Highlight xe nên tap tiếp theo.
- Nên lấy từ solver hoặc hint path được author trước.

## 11. Economy Và Monetization

Soft currency: Coins

- Nhận khi win level, giữ streak, daily reward, hard level bonus.
- Dùng để mua booster và cosmetic bus skin.

Stars:

- Lớp mastery tùy chọn: 3 sao dựa trên không dùng booster, ít move, clear nhanh.
- Không nên tạo áp lực timer ở early game.

Ads:

- Interstitial sau mỗi 2-3 level thắng, không hiện sau fail trong 10 level đầu.
- Rewarded ads cho Extra Station, Undo pack, x2 coins, revive khi deadlock.
- Không khuyến nghị banner trong gameplay vì làm giảm độ đọc của puzzle.

IAP:

- Remove Ads.
- Starter Pack: coins + booster + bus skin riêng.
- Booster bundle.
- Cosmetic city theme hoặc bus pack.

## 12. Progression

Cấu trúc đề xuất:

- Linear level ladder cho MVP.
- Mỗi 5 level: puzzle khó hơn, thưởng tốt hơn.
- Mỗi 10 level: đổi visual thành phố/trạm.
- Daily challenge mở sau level 20.
- Event board mở sau level 35.

Retention hook:

- Day 1: daily reward đơn giản.
- Day 3: mở cosmetic bus skin.
- Day 7: weekly challenge chest.
- Streak saver bằng rewarded ad.

## 13. UI/UX Screen

Screen bắt buộc cho MVP:

- Splash/loading.
- Main menu có Play, level number, coins, settings.
- Gameplay HUD: level, coins, nút booster, restart, settings.
- Win popup: reward, next button, x2 reward ad.
- Fail/deadlock popup: undo, extra station, restart.
- Shop popup: booster, coins, remove ads.
- Settings: sound, music, haptics, privacy, restore purchases.

Nguyên tắc HUD gameplay:

- Giữ bàn chơi sạch và dễ đọc.
- Đặt booster gần vùng bấm ngón tay, thường ở đáy màn hình.
- Dùng icon rõ ràng, hạn chế text dài trong gameplay.
- Hỗ trợ colorblind: thêm symbol trên nóc xe và mũ/áo hành khách.

## 14. Art Direction

Camera:

- Màn dọc, orthographic 3D, góc nhìn hơi từ trên xuống.
- Toàn bộ bàn puzzle phải nằm trong màn hình.

Style:

- 3D toy-like, hình khối tròn mềm, màu tươi nhưng không quá chói.
- Nhận diện riêng: biển trạm, họa tiết xe, prop theo từng thành phố.

Bảng màu:

- Dùng 6-8 màu gameplay có độ tách biệt cao: đỏ, xanh dương, vàng, xanh lá, tím, cam, cyan, hồng.
- Thêm pattern/symbol để hỗ trợ khả năng nhận diện: tròn, sọc, tam giác, sao, kim cương.

Animation:

- Tap bounce xe: 0.08-0.12 giây.
- Xe chạy vào trạm: 0.45-0.8 giây tùy khoảng cách.
- Khách lên/xuống: stagger 0.05-0.08 giây mỗi khách.
- Xe rời bàn: 0.5 giây, có trail/confetti nhẹ khi hoàn thành.

Audio:

- Tap click mềm.
- Engine loop ngắn chỉ khi xe di chuyển.
- Boarding pop.
- Cheer khi xe đầy.
- Deadlock low tone.
- Tất cả âm thanh cần ngắn, không gây mệt khi lặp lại.

## 15. Technical Design Cho Unity

Module Unity đề xuất:

- `LevelManager`: load level data và điều khiển win/loss.
- `BoardController`: quản lý station, lane, path graph và occupancy.
- `BusController`: state machine và animation di chuyển của xe.
- `PassengerController`: animation và state của hành khách.
- `MoveResolver`: validate tap và xử lý chuỗi unload/load/exit.
- `PuzzleSolver`: tool/runtime tùy chọn cho hint và validate level.
- `BoosterService`: xử lý undo, extra station, hint, swap, paint.
- `EconomyService`: coin, reward, purchase.
- `AdService`: interstitial/rewarded placement.
- `SaveService`: progress, currency, settings.

State machine của bus:

- Idle
- Selected
- MovingToStation
- Unloading
- Loading
- Completed
- Exiting
- Blocked

Thứ tự xử lý một move:

1. Validate xe có thể di chuyển.
2. Reserve trạm đích.
3. Chạy animation xe.
4. Cho khách sai màu xuống.
5. Cho khách đúng màu lên.
6. Kiểm tra xe đã hoàn thành chưa.
7. Giải phóng hoặc cập nhật trạng thái trạm.
8. Kiểm tra win/deadlock.

## 16. Schema Dữ Liệu Level

Dạng JSON đề xuất:

```json
{
  "levelId": 1,
  "colors": ["red", "blue"],
  "stations": [
    { "id": "S1", "x": 0, "y": 0 }
  ],
  "buses": [
    {
      "id": "B1",
      "color": "red",
      "capacity": 3,
      "laneId": "L1",
      "startIndex": 0,
      "passengers": ["red", "blue"]
    }
  ],
  "waitingPassengers": [
    { "color": "red", "queueId": "Q1", "count": 3 }
  ],
  "blockers": [],
  "target": { "clearAllBuses": true },
  "moveLimit": null
}
```

## 17. Analytics Event

Core:

- `level_start(levelId, attempt)`
- `bus_tap(levelId, busColor, valid, reasonIfInvalid)`
- `move_resolved(levelId, unloadedCount, loadedCount, completedBus)`
- `booster_used(levelId, boosterType, source)`
- `deadlock(levelId, movesMade)`
- `level_complete(levelId, duration, moves, boostersUsed)`
- `level_fail(levelId, duration, reason)`

Monetization:

- `ad_offer_shown(placement)`
- `ad_started(placement)`
- `ad_completed(placement)`
- `iap_started(productId)`
- `iap_completed(productId)`

KPI cần theo dõi:

- Tỉ lệ hoàn thành level.
- Số attempt trung bình mỗi level.
- Tỉ lệ deadlock.
- Tần suất dùng booster mỗi level.
- D1/D3/D7 retention.
- Tần suất impression interstitial.
- Rewarded ad opt-in rate.

## 18. Phạm Vi MVP

MVP cần có:

- 50 level hand-authored.
- 5 màu gameplay.
- 3 loại sức chứa xe.
- 1 loại station.
- Booster: Undo, Extra Station, Hint.
- Coin và shop booster cơ bản.
- Hook interstitial và rewarded ad.
- Flow win/fail đơn giản.
- Save progress.
- Toggle sound/music/haptic.

Sau MVP:

- 200+ level.
- Daily challenge.
- Cosmetic skin.
- Event ladder.
- Cloud save.
- Tool tạo level.
- Solver để validate level.

## 19. Milestone Sản Xuất

Prototype, 3-5 ngày:

- Tap xe, xe vào station, load/unload, win condition.
- 10 level test.

Vertical slice, 1-2 tuần:

- Full gameplay loop, HUD, 30 level, booster, save, polish cơ bản.

Soft launch, 3-5 tuần:

- 150 level, ads/IAP, analytics, balance tuning, onboarding, store assets.

Live iteration:

- Thêm 50-100 level mới mỗi tuần ở giai đoạn đầu.
- Tune ad frequency theo retention.
- Thêm event variant sau khi metric core ổn định.

## 20. Rủi Ro Thiết Kế Chính

- Rủi ro: game quá tự động, người chơi không thấy mình ra quyết định. Giải pháp: giới hạn station, tạo phụ thuộc thứ tự xe, hiện rõ hậu quả của nước đi.
- Rủi ro: khó phân biệt màu. Giải pháp: thêm symbol/pattern và bảng màu tương phản cao.
- Rủi ro: deadlock gây ức chế. Giải pháp: feedback rõ, Undo, hint logic, solver validation.
- Rủi ro: ads quá dày làm giảm retention. Giải pháp: không forced ads sớm, rewarded ads gắn với giá trị cụ thể.
- Rủi ro: quá giống game tham chiếu. Giải pháp: art riêng, UI riêng, level gốc, progression riêng, store presentation gốc.

## 21. Tiêu Chí Chấp Nhận MVP

MVP sẵn sàng khi:

- Người chơi mới hiểu level đầu mà không cần đọc hướng dẫn dài.
- Level 1-10 có tỉ lệ hoàn thành trên 90% trong test nội bộ.
- Không level nào bắt buộc dùng booster.
- Deadlock detection hoạt động ổn định.
- Undo khôi phục board state chính xác.
- Animation load/move không bao giờ khóa input vĩnh viễn.
- Game chạy 60 FPS trên Android tầm trung phổ biến.
- Art, UI và copy đều là nội dung gốc, không copy từ game tham chiếu.

