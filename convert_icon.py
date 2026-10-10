import sys
from PIL import Image

def png_to_ico(png_path, ico_path):
    img = Image.open(png_path).convert("RGBA")
    
    # Resize to square (256x256 is good for Windows ICO)
    # We can preserve aspect ratio by pasting on a transparent square
    w, h = img.size
    max_dim = max(w, h)
    new_img = Image.new("RGBA", (max_dim, max_dim), (0, 0, 0, 0))
    paste_x = (max_dim - w) // 2
    paste_y = (max_dim - h) // 2
    new_img.paste(img, (paste_x, paste_y))
    
    # Save as ICO (multiple sizes)
    new_img.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (32, 32), (16, 16)])

if __name__ == "__main__":
    src_file = "h:/CRM MINI/ScripOx.Desktop/bin/Debug/net8.0-windows/win-x64/Resources/fivcon.png"
    dest_file = "h:/CRM MINI/ScripOx.Desktop/Resources/Icons/app.ico"
    png_to_ico(src_file, dest_file)
    print("Icon generated successfully!")
