const input = document.getElementById("input");
const output = document.getElementById("output");

let controller;

input.addEventListener("input", async () => {
  controller?.abort();
  controller = new AbortController();

  try {
    const response = await fetch("linkernize", { method: "POST", body: input.value, signal: controller.signal });
    if (response.ok) {
      output.innerHTML = await response.text();
      return;
    }
    console.error(`Request failed with status ${response.status}`);
  } catch (error) {
    if (error.name === "AbortError") {
      return;
    }
    console.error(error);
  }
  output.textContent = "Could not linkernize the input.";
});

input.dispatchEvent(new Event("input"));
