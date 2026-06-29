# Paper Legends - Đấu Tướng Giấy

`Paper Legends` là dự án game online mới được clone từ nền tảng game bắn bi online cũ. Mục tiêu của nhánh mới là chuyển toàn bộ nền multiplayer, server, tài khoản, shop, admin web và tooling hiện có sang một board game hành động lấy cảm hứng từ trò chơi tự chế thời nhỏ: nhân vật vẽ trên giấy cứng, cắt rời, rồi di chuyển bằng lực bấm/ngòi bút.

Tên ý tưởng nội bộ: `Bún Giấy` / `Đấu Tướng Giấy`.

## Tầm nhìn game

Người chơi chọn một tướng giấy. Mỗi tướng là một nhân vật được thiết kế như mô hình giấy cắt rời, có ngoại hình riêng, chỉ số riêng và một kỹ năng đặc trưng. Thay vì điều khiển bằng joystick theo kiểu chạy liên tục, nhân vật di chuyển theo cơ chế "búng bằng bút": người chơi chọn cạnh/điểm tác động, giữ lực, thả ra để nhân vật bật nhẹ lên, trượt/nhảy một đoạn, rồi rơi xuống bàn chơi.

Luật tiêu diệt cốt lõi rất trực quan: nếu tướng giấy của bạn rơi xuống và đè lên tướng giấy đối phương, đối phương bị hạ ngay lập tức.

Gameplay lấy cảm giác chiến thuật từ MOBA như Liên Quân nhưng thu gọn thành một đường duy nhất, nhịp chơi nhanh, dễ hiểu, có kỹ năng tướng và mục tiêu phá nhà.

## Thể loại

- Online board game hành động.
- Đấu tướng giấy theo lượt ngắn hoặc bán real-time tùy mode.
- Một đường duy nhất, có nhà chính hai bên.
- Hỗ trợ solo và đội 3v3.
- Tập trung vào vật lý, căn lực, kỹ năng và va chạm.

## Cấu trúc workspace

Workspace mới nằm tại:

```text
E:\my project\PaperLegendsProject
```

Các project chính:

```text
PaperLegendsProject
|-- PaperLegends          # Unity client dành cho người chơi
|-- PaperLegend_Server    # Unity dedicated/server logic dùng Photon Fusion 2
|-- WEB_SERVER            # Web admin/backend quản lý game cơ bản
`-- PaperLegends.code-workspace
```

### PaperLegends

Client Unity của game. Đây là nơi xử lý:

- Giao diện người chơi.
- Điều khiển tướng giấy.
- Camera, hiệu ứng, âm thanh.
- Hiển thị map, nhà chính, lane, tướng, kỹ năng.
- Kết nối tới server Unity/Fusion và backend web.
- Addressables và asset phía client.

Thông tin hiện tại:

- Unity: `6000.0.77f1`
- Product name: `Paper Legends`
- Android/Standalone identifier: `com.gamenhalam.paperlegends`
- Photon Fusion đang kế thừa từ dự án bắn bi cũ.

### PaperLegend_Server

Unity server/dedicated project quản lý logic trận đấu online bằng Photon Fusion 2.

Vai trò dự kiến:

- Tạo và quản lý phòng đấu.
- Đồng bộ trạng thái tướng giấy.
- Xác thực lực búng, hướng búng, va chạm và kết quả hạ gục.
- Quản lý điểm hạ gục, phá nhà, kết thúc trận.
- Chống gian lận các thao tác quan trọng.
- Hỗ trợ reconnect/host migration nếu còn giữ cơ chế cũ.

### WEB_SERVER

Backend và web admin quản lý dữ liệu game.

Vai trò dự kiến:

- Quản lý người chơi, đăng nhập, hồ sơ.
- Quản lý tướng giấy, kỹ năng, chỉ số, skin.
- Quản lý item, shop, tài nguyên, phần thưởng.
- Quản lý cấu hình trận đấu, leaderboard, lịch sử trận.
- Cung cấp API cho Unity client và server.
- Admin web để nhập/sửa dữ liệu game cơ bản.

## Luật chơi cốt lõi

### 1. Chọn tướng

Mỗi người chơi chọn một tướng giấy trước khi vào trận. Mỗi tướng có:

- Hình dáng giấy riêng.
- Kích thước/hitbox riêng.
- Trọng lượng và độ nảy riêng.
- Kỹ năng chủ động hoặc bị động riêng.
- Vai trò chiến thuật riêng như sát thủ, đỡ đòn, khống chế, hỗ trợ hoặc phá nhà.

### 2. Di chuyển bằng lực búng

Người chơi chọn điểm/cạnh trên tướng, kéo hoặc giữ lực, sau đó thả để tạo cú búng.

Cú búng cần mô phỏng cảm giác ngoài đời:

- Bấm vào cạnh nhân vật bằng ngòi bút.
- Nhân vật bật nhẹ lên khỏi mặt bàn.
- Nhân vật bay/trượt một đoạn ngắn.
- Nhân vật rơi xuống và dừng theo vật lý.

Thông số cần mô phỏng:

- Hướng lực.
- Độ mạnh.
- Điểm đặt lực.
- Ma sát mặt bàn.
- Độ nảy.
- Trọng lượng tướng.
- Va chạm với tướng, trụ, nhà, lính hoặc vật cản.

### 3. Hạ gục

Một tướng bị hạ nếu tướng đối phương rơi xuống và đè lên vùng thân/hitbox hợp lệ.

Nguyên tắc dự kiến:

- Chỉ tính hạ gục khi có trạng thái "đang rơi" hoặc "landing attack".
- Va chạm ngang có thể gây đẩy lùi nhưng không nhất thiết hạ gục.
- Tướng bị đè sẽ chết ngay, sau đó hồi sinh tại căn cứ sau thời gian chờ.
- Người hạ gục được cộng điểm kill và có thể nhận tài nguyên trận.

### 4. Kỹ năng tướng

Mỗi tướng có một kỹ năng riêng để tạo chiều sâu chiến thuật. Ví dụ:

- Bật nhảy thêm một lần sau khi rơi.
- Tăng lực búng trong lượt tiếp theo.
- Tạo vùng keo làm chậm đối thủ.
- Dựng khiên giấy chặn va chạm.
- Đổi hướng nhẹ khi đang bay.
- Tàng hình ngắn trước khi búng.
- Cú rơi nặng gây choáng quanh điểm đáp.

Kỹ năng cần được server xác thực để tránh gian lận.

### 5. Map một đường

Trận đấu diễn ra trên một lane duy nhất. Hai đội xuất phát ở hai đầu bàn chơi.

Thành phần map dự kiến:

- Nhà chính mỗi bên.
- Có thể có trụ hoặc chướng ngại giấy.
- Có thể có lính giấy đi theo lane.
- Khu vực giữa map là nơi tranh chấp chính.
- Có thể có vùng đặc biệt như keo, nước, gió, giấy nhám, lỗ thủng.

### 6. Điều kiện thắng

Trận kết thúc khi một trong các điều kiện xảy ra:

- Phá nhà chính của đối phương.
- Một đội đạt `20` mạng hạ gục.
- Hết thời gian trận, đội có nhiều điểm hơn thắng.

Mode ban đầu:

- Solo: `1v1`
- Đội nhỏ: `3v3`

## Hướng chuyển đổi từ game bắn bi cũ

Project hiện tại vẫn còn nhiều code, asset, tên biến và tài liệu kế thừa từ game bắn bi online. Khi chuyển đổi sang Paper Legends, ưu tiên xử lý theo thứ tự:

1. Giữ lại nền online, đăng nhập, phòng đấu, matchmaking, backend, admin web.
2. Thay cơ chế điều khiển bi bằng cơ chế búng tướng giấy.
3. Thay model bi bằng nhân vật giấy/hitbox giấy.
4. Thay luật thắng/thua bắn bi bằng luật phá nhà/20 kills.
5. Thay dữ liệu item/forge bi bằng dữ liệu tướng, kỹ năng, skin và nâng cấp.
6. Dọn dần tên cũ như `BanBi`, `BanCuli`, `Ball`, `Marble` khi code liên quan được chuyển đổi thật.

Không nên rename hàng loạt class cũ khi chưa hiểu dependency, vì dễ làm hỏng prefab, serialized field và network object trong Unity.

## Networking

Project dùng Photon Fusion 2 để đồng bộ trận đấu online.

Định hướng authority:

- Client gửi input: hướng búng, lực búng, điểm tác động, dùng kỹ năng.
- Server/Fusion xác thực input và mô phỏng kết quả quan trọng.
- Kết quả hạ gục, máu nhà, điểm kill và kết thúc trận phải do server quyết định.
- Client chỉ hiển thị dự đoán, hiệu ứng và UI.

Các luồng cần ưu tiên ổn định:

- Tạo phòng.
- Join/rejoin phòng.
- Spawn tướng.
- Đồng bộ lượt hoặc pha hành động.
- Xác nhận hạ gục.
- Đồng bộ điểm số và trạng thái nhà chính.
- Kết thúc trận và gửi kết quả về backend.

## Backend và admin

`WEB_SERVER` tiếp tục là nơi quản lý dữ liệu live game. Dữ liệu cần có cho Paper Legends:

- Danh sách tướng.
- Chỉ số vật lý của từng tướng.
- Kỹ năng và cooldown.
- Skin/ngoại hình giấy.
- Cấu hình map.
- Cấu hình mode `1v1`, `3v3`.
- Phần thưởng trận.
- Leaderboard.
- Lịch sử trận.

Trong giai đoạn đầu, có thể giữ endpoint cũ nếu client/server vẫn phụ thuộc vào chúng. Khi gameplay mới ổn định, đổi tên API và schema theo domain mới.

## Cài đặt và chạy

### Mở client

1. Mở Unity Hub.
2. Add project: `E:\my project\PaperLegendsProject\PaperLegends`
3. Dùng Unity `6000.0.77f1`.
4. Kiểm tra `ProjectSettings/ProjectVersion.txt` nếu Unity yêu cầu nâng/hạ version.
5. Mở scene client chính và chạy Play Mode.

### Mở server Unity

1. Add project: `E:\my project\PaperLegendsProject\PaperLegend_Server`
2. Dùng cùng version Unity với client nếu có thể.
3. Kiểm tra Photon App Settings và server config.
4. Build dedicated server hoặc chạy server scene trong Editor tùy workflow hiện tại.

### Mở web admin/backend

1. Mở terminal tại `E:\my project\PaperLegendsProject\WEB_SERVER`.
2. Cài dependency theo package manager của project.
3. Cấu hình `.env` nếu backend yêu cầu.
4. Chạy backend/admin theo script có sẵn trong `package.json`.

## Cấu hình server trong client

Client hiện dùng asset cấu hình server trong:

```text
Assets/MyFusionGame_Shared/Resources/ServerConfig.asset
```

Các field quan trọng:

- `baseUrlPhoton`: địa chỉ server Photon/dedicated.
- `baseUrl`: API backend production.
- `baseUrlLocal`: API backend local.
- `webSocketUrl`: websocket backend nếu còn dùng.
- `catalogUrl`: addressables catalog.

Khi đổi môi trường, ưu tiên sửa asset config trong Unity Inspector thay vì hardcode trong script.

## Ghi chú dành cho AI/Codex

- Đây là project mới clone từ game bắn bi online cũ.
- Mục tiêu sản phẩm mới là `Paper Legends` / `Đấu Tướng Giấy`, không còn là game bắn bi.
- Không sửa file `.unity` nếu không được yêu cầu rõ.
- Không rename hàng loạt class/prefab/serialized field khi chưa kiểm tra reference.
- Khi gặp tên cũ trong code, chỉ đổi nếu phần đó đang được chuyển đổi sang gameplay mới.
- Client nằm ở `PaperLegends`.
- Unity server nằm ở `PaperLegend_Server`.
- Web admin/backend nằm ở `WEB_SERVER`.
- Nếu cần test nhanh, ưu tiên compile/check script trước; Play Mode có thể bỏ qua nếu chưa có scene chuyển đổi.

## Roadmap chuyển đổi

### Giai đoạn 1 - Định danh và tài liệu

- Đổi tên project, product name, package id sang Paper Legends.
- Viết lại README và tài liệu gameplay.
- Xác định các module cũ cần giữ: login, phòng, matchmaking, inventory, admin.
- Xác định các module cần thay: ball physics, marble rules, ball forge, old win condition.

### Giai đoạn 2 - Prototype gameplay

- Tạo tướng giấy đầu tiên với hitbox rõ ràng.
- Tạo input búng bằng lực.
- Mô phỏng bật nhẹ, bay/trượt, rơi xuống.
- Xác định va chạm đè lên đối thủ.
- Làm mode test `1v1` local hoặc host-client đơn giản.

### Giai đoạn 3 - Online match

- Spawn tướng qua Fusion.
- Đồng bộ input búng.
- Server xác thực landing kill.
- Đồng bộ hồi sinh, điểm kill, máu nhà.
- Kết thúc trận khi phá nhà hoặc đạt 20 kills.

### Giai đoạn 4 - Nội dung game

- Tạo danh sách tướng giấy.
- Mỗi tướng có kỹ năng riêng.
- Tạo map một đường.
- Tạo nhà chính, trụ/chướng ngại nếu cần.
- Tạo UI chọn tướng, HUD trận, bảng điểm.

### Giai đoạn 5 - Backend/admin

- Tạo schema tướng, kỹ năng, skin.
- Tạo API cấu hình game.
- Tạo admin CRUD cho tướng/kỹ năng.
- Gửi kết quả trận về backend.
- Tạo leaderboard cho solo và 3v3.

## Nguyên tắc thiết kế

- Cảm giác búng phải là linh hồn của game.
- Luật hạ gục phải dễ hiểu: rơi xuống đè trúng là chết.
- Mỗi tướng cần khác nhau thật, không chỉ khác chỉ số.
- Trận phải ngắn, rõ mục tiêu, dễ xem lại.
- Vật lý vui nhưng kết quả online phải đủ ổn định và chống gian lận.

## Trạng thái hiện tại

Project đang ở giai đoạn clone/chuyển đổi. Nhiều tên cũ, asset cũ và logic cũ vẫn có thể còn tồn tại trong source. README này là tài liệu định hướng mới cho Paper Legends để các lần sửa tiếp theo bám đúng mục tiêu sản phẩm.



bạn biết full kỹ thuật trong photo fusion 2 khi truyền data giữ server và client chứ ?
để mình nói nhé:
gồm:
1. tạo biến trên 1 object netwwork dùng chung => lúc này toàn bộ client trong phòng join cùng nhau sẽ đều nhìn thấy biến này và giá trị nó . tuy nhiên vì giới hạn nên không thể lấy được data lớn.
2. Dùng kỹ thuật bắn RPC: bắn 1 tín hiệu lên server từ client xử lý. không ổn định ddo mạng và đôi khi bị trễ lag mất tín hiệu ít khuyên xài
3. [Networked, OnChangedRender(nameof(OnHeldPositionChanged))]  1 kỹ thuật khai báo biến khi mà nó đổi thì hàm được gắn sẽ gọi toàn bộ client
4. Kỹ thuật khai báo 1 hàm để gọi cho cả client và server:
khi muốn gọi 1 hàm mà muốn cả 2 đều sử dụng thì phía client bọc thêm #if UNITY_SERVER