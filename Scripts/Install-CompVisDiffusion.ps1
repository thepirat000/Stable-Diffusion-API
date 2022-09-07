# https://www.assemblyai.com/blog/how-to-run-stable-diffusion-locally-to-generate-images/
# https://www.howtogeek.com/830179/how-to-run-stable-diffusion-on-your-pc-to-generate-ai-images/

Set-ExecutionPolicy Bypass -Scope Process -Force; 

## 1. Pre-requisites

# Install chocolatey
iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'));

refreshenv

# Install ffmpeg
Write-Host "Installing ffmpeg" -foregroundcolor "green";
choco install ffmpeg -y --no-progress

# Install GIT
Write-Host "Installing GIT" -foregroundcolor "green";
choco install git -y --no-progress

# choco install cuda --version=11.4 -y --no-progress
$file = './cuda_11.4.1_471.41_win10.exe';
Start-BitsTransfer -Source https://developer.download.nvidia.com/compute/cuda/11.4.1/local_installers/cuda_11.4.1_471.41_win10.exe -Destination $file
& $file -y --no-progress

Read-Host "Complete the CUDA installation and then press enter to continue..."

$file = './473.81-data-center-tesla-desktop-win10-win11-64bit-dch-international.exe';
Start-BitsTransfer -Source https://us.download.nvidia.com/tesla/473.81/473.81-data-center-tesla-desktop-win10-win11-64bit-dch-international.exe -Destination $file
& $file -y --no-progress

Read-Host "Complete the NVidia driver installation and then press enter to continue..."

refreshenv

# Configure GPU
# (Not sure if needed) 
# & "C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi" -fdm 0

# Install Conda
Write-Host "Installing miniconda3 (this can take some time)" -foregroundcolor "green";
choco install miniconda3 -y --no-progress

## 2. Install Stable diffusion

git clone https://github.com/CompVis/stable-diffusion.git
cd stable-diffusion/

# Download checkpoints
Start-Process https://drive.google.com/file/d/1hdHZbvl7anB9HddtFFpIp-mmTj8EpkgC/view?usp=sharing
Start-Process https://drive.google.com/file/d/138rWFAQYlbv5lgfax_n0zxEYpUEA7bh4/view?usp=sharing
Start-Process https://drive.google.com/file/d/1nGpd4cnTjo11Un7KCX46-OpGciVMO-eD/view?usp=sharing
Start-Process https://drive.google.com/file/d/1G8QcOOG-gKlHipT-37PhjIYYx2P-1jXZ/view?usp=sharing

Read-Host "Complete checkpoint downloads, copy the .ckpt to stable-diffusion/ folder. Press enter to continue..."

& 'C:\tools\miniconda3\shell\condabin\conda-hook.ps1'; 
conda activate 'C:\tools\miniconda3';
conda env create -f environment.yaml
conda activate ldm

## 4. Command samples

# 1 sample, 65 steps, v1-4
python scripts/txt2img.py --prompt "A dinosaur in the style of Vincent van Gogh" --plms --ckpt sd-v1-4.ckpt --skip_grid --n_samples 1 --n_iter 1 --ddim_steps 65 --seed 12345

# 1 sample, 65 steps, v1-2
python scripts/txt2img.py --prompt "A dinosaur in the style of Vincent van Gogh" --plms --ckpt sd-v1-2.ckpt --skip_grid --n_samples 1 --n_iter 1 --ddim_steps 65 --seed 12345

# 2 samples, 50 steps, v1-3
python scripts/txt2img.py --prompt "A giraffe on an airplane" --plms --ckpt sd-v1-3.ckpt --skip_grid --n_samples 1 --n_iter 2 --ddim_steps 50 --seed 65537

# 2 samples, 65 steps, v1-4
python scripts/txt2img.py --prompt "A rock band playing under water" --plms --ckpt sd-v1-4.ckpt --skip_grid --n_samples 1 --n_iter 2 --ddim_steps 65 --seed 32
