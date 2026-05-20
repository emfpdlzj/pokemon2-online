import { bootstrapClient } from "./app/bootstrap.js";

bootstrapClient().catch(error => {
  console.error(error);
  const status = document.getElementById("menu-status");
  if (status) status.textContent = "게임 데이터를 불러오지 못했습니다. 정적 서버 실행 상태를 확인하세요.";
});
