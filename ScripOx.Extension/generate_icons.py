import sys
from PIL import Image

def pad_to_square(img_path, out_path, size):
    img = Image.open(img_path).convert("RGBA")
    w, h = img.size
    max_dim = max(w, h)
    
    # Create a new transparent square image
    new_img = Image.new("RGBA", (max_dim, max_dim), (0, 0, 0, 0))
    
    # Paste the original image into the center
    paste_x = (max_dim - w) // 2
    paste_y = (max_dim - h) // 2
    new_img.paste(img, (paste_x, paste_y))
    
    # Resize to the target size
    final_img = new_img.resize((size, size), Image.Resampling.LANCZOS)
    final_img.save(out_path, "PNG")

if __name__ == "__main__":
    src_file = "h:/CRM MINI/ScripOx.Extension/public/assets/main_logo.png"
    sizes = [16, 32, 48, 128]
    for s in sizes:
        pad_to_square(src_file, f"h:/CRM MINI/ScripOx.Extension/public/icons/icon{s}.png", s)
        pad_to_square(src_file, f"h:/CRM MINI/ScripOx.Extension/icons/icon{s}.png", s)
    print("Done generating icons.")
