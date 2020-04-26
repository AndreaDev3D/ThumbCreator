ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4

ffmpeg -r 64 -s 512x512 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png ../../../_Gif/output.gif
ffmpeg -r 50 -s 1024x1024 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png ../../../_Gif/output2.gif

ffmpeg -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png ../../../_Video/test.avi

set /p DUMMY=Hit ENTER to continue...