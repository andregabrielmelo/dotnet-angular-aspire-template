import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AVATAR_MAX_BYTES, AvatarEditor } from './avatar-editor';
import { UsersService } from '../users/users.service';

describe('AvatarEditor', () => {
  function setup(users: Partial<Record<keyof UsersService, ReturnType<typeof vi.fn>>>) {
    TestBed.configureTestingModule({
      imports: [AvatarEditor],
      providers: [{ provide: UsersService, useValue: users }],
    });
    const fixture = TestBed.createComponent(AvatarEditor);
    fixture.componentRef.setInput('userId', 7);
    fixture.detectChanges();
    return fixture;
  }

  function fileEvent(file: File): Event {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });
    return { target: input } as unknown as Event;
  }

  it('shows a placeholder when the user has no avatar', () => {
    const fixture = setup({
      getAvatar: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
    });

    expect(fixture.componentInstance.imageUrl()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Upload image');
  });

  it('refuses a file over the size limit without calling the API', () => {
    const uploadAvatar = vi.fn();
    const fixture = setup({
      getAvatar: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
      uploadAvatar,
    });

    fixture.componentInstance.upload(
      fileEvent(new File([new Uint8Array(AVATAR_MAX_BYTES + 1)], 'big.png')),
    );

    expect(uploadAvatar).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('2 MB');
  });

  it('shows the API reason when an upload is refused', () => {
    const fixture = setup({
      getAvatar: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
      uploadAvatar: vi.fn().mockReturnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { errors: { file: ['The file must be a JPEG, PNG or WebP image.'] } },
            }),
        ),
      ),
    });

    fixture.componentInstance.upload(fileEvent(new File(['not an image'], 'a.png')));

    expect(fixture.componentInstance.errorMessage()).toBe(
      'The file must be a JPEG, PNG or WebP image.',
    );
  });

  it('uploads, then shows the stored image', () => {
    const getAvatar = vi
      .fn()
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 404 })))
      .mockReturnValueOnce(of(new Blob(['x'], { type: 'image/webp' })));
    const uploadAvatar = vi.fn().mockReturnValue(of(undefined));
    URL.createObjectURL = vi.fn().mockReturnValue('blob:avatar');
    URL.revokeObjectURL = vi.fn();
    const fixture = setup({ getAvatar, uploadAvatar });

    fixture.componentInstance.upload(fileEvent(new File(['png'], 'a.png')));

    expect(uploadAvatar).toHaveBeenCalledWith(7, expect.any(File));
    expect(fixture.componentInstance.imageUrl()).toBe('blob:avatar');
  });
});
