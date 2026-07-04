const input = document.getElementById("input");
const output = document.getElementById("output");

async function linkernize() {
  const response = await fetch("linkernize", { method: "POST", body: input.value });
  output.innerHTML = await response.text();
}

input.addEventListener("input", linkernize);

_ = linkernize();
